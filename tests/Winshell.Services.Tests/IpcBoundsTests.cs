using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Winshell.Services.Tests;

[TestClass]
public class IpcBoundsTests
{
    [TestMethod]
    public async Task An_unfinished_line_stops_at_the_limit_and_shutdown_cancels_the_read()
    {
        var name = PipeName();
        var loop = new EventQueue();
        var control = new FakeInstanceControl();
        var server = new IpcServer(IpcServer.OpenFirst(name), name, loop, Log.Default, control);
        var shutDown = false;
        try
        {
            await using var client = await Connect(name);

            var payload = new byte[IpcServer.MaxRequestBytes + 1];
            Array.Fill(payload, (byte)'a');
            var writing = Task.Run(() =>
            {
                try
                {
                    client.Write(payload);
                }
                catch (IOException)
                {
                }
            }, CurrentTest.Cancellation);
            using var reader = new StreamReader(client, Encoding.UTF8, false, 256, true);
            var line = await reader.ReadLineAsync(CurrentTest.Cancellation).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(10), CurrentTest.Cancellation);
            Assert.IsNotNull(line);
            var response = JsonSerializer.Deserialize(line, IpcJson.Default.IpcResponse);
            Assert.IsNotNull(response);
            Assert.IsFalse(response.Ok);
            Assert.AreEqual("Request is too long.", response.Error);
            await writing.WaitAsync(TimeSpan.FromSeconds(5), CurrentTest.Cancellation);

            await using var parked = await Connect(name);
            var held = new byte[IpcServer.MaxRequestBytes - 1];
            var prefix = "{\"op\":\"stop\""u8;
            prefix.CopyTo(held);
            Array.Fill(held, (byte)' ', prefix.Length, held.Length - prefix.Length);
            await parked.WriteAsync(held, CurrentTest.Cancellation);
            server.Dispose();
            shutDown = true;

            var n = await parked.ReadAsync(new byte[1], CurrentTest.Cancellation).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), CurrentTest.Cancellation);
            Assert.AreEqual(0, n);
            try
            {
                await parked.WriteAsync("}\n"u8.ToArray(), CurrentTest.Cancellation);
            }
            catch (IOException)
            {
            }

            loop.RunDue();
            Assert.AreEqual(0, control.Quits);
        }
        finally
        {
            if (!shutDown)
                server.Dispose();
        }
    }

    [TestMethod]
    public async Task A_listener_that_leaves_without_a_signal_frees_its_place()
    {
        using var probe = new Probe();
        var held = new List<NamedPipeClientStream>();
        try
        {
            await Fill(probe, held, IpcServer.MaxClients);
            await held[0].DisposeAsync();

            var taken = false;
            await Wait.UntilAsync(() => taken, "The place of the listener that left was never freed.", async () =>
            {
                try
                {
                    await using var next = await Connect(probe.Pipe);
                    await next.WriteAsync(ListenRequest, CurrentTest.Cancellation);
                    var line = ReadLine(next);
                    await probe.WaitUntil(() =>
                    {
                        probe.Ping.Emit("sync");
                        return line.IsCompleted;
                    }, "A client after the listener left was neither served nor closed.");
                    taken = await line is not null;
                }
                catch (IOException)
                {
                }
            });
        }
        finally
        {
            foreach (var client in held)
                await client.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task A_connection_past_the_cap_is_closed()
    {
        using var probe = new Probe();
        var held = new List<NamedPipeClientStream>();
        try
        {
            await Fill(probe, held, IpcServer.MaxClients);

            await using var extra = await Connect(probe.Pipe);
            var n = await extra.ReadAsync(new byte[1], CurrentTest.Cancellation).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), CurrentTest.Cancellation);
            Assert.AreEqual(0, n);
        }
        finally
        {
            foreach (var client in held)
                await client.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task Clients_that_send_nothing_give_their_slots_back()
    {
        var name = PipeName();
        var loop = new ManualEventLoop();
        using var server = new IpcServer(IpcServer.OpenFirst(name), name, loop, Log.Default, new FakeInstanceControl());
        var idle = new List<NamedPipeClientStream>();
        try
        {
            for (var i = 0; i < IpcServer.MaxClients; i++)
                idle.Add(await ConnectWhenFree(name));

            var answered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var asking = Task.CompletedTask;
            await Wait.UntilAsync(() => answered.Task.IsCompleted, "The idle clients never gave their slots back.",
                () =>
                {
                    loop.Advance(IpcServer.RequestTimeout);
                    if (asking.IsCompleted)
                        asking = Ask(name, answered);
                });
            var answer = await answered.Task;
            var response = JsonSerializer.Deserialize(answer, IpcJson.Default.IpcResponse);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.Ok);
        }
        finally
        {
            foreach (var client in idle)
                await client.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task A_listener_that_falls_behind_keeps_only_the_signal_buffer()
    {
        using var probe = new Probe();
        await using var client = await Connect(probe.Pipe);
        await Subscribe(probe, client);

        var value = new string('a', 128 * 1024);
        probe.Ping.Emit(value);
        var first = new byte[1];
        Assert.AreEqual(1,
            await client.ReadAsync(first, CurrentTest.Cancellation).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(10), CurrentTest.Cancellation));
        for (var i = 0; i < IpcServer.MaxQueuedSignals + 20; i++)
            probe.Ping.Emit("queued");

        var lines = await ReadLines(client, 1 + IpcServer.MaxQueuedSignals, first[0]);
        Assert.AreEqual(1 + IpcServer.MaxQueuedSignals, lines.Count);
        var values = lines.Select(line =>
        {
            var response = JsonSerializer.Deserialize(line, IpcJson.Default.IpcResponse);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.Ok);
            return response.Value;
        }).ToList();
        Assert.AreEqual(value, values[0]);
        foreach (var queued in values.Skip(1))
            Assert.AreEqual("queued", queued);
        await Assert.ThrowsExactlyAsync<TimeoutException>(async () =>
            await client.ReadAsync(new byte[1], CurrentTest.Cancellation).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2), CurrentTest.Cancellation));
    }

    private static readonly byte[] ListenRequest =
        "{\"op\":\"listen\",\"target\":\"probe\",\"name\":\"ping\"}\n"u8.ToArray();

    private static async Task Subscribe(Probe probe, Stream client)
    {
        await client.WriteAsync(ListenRequest, CurrentTest.Cancellation);
        var line = ReadLine(client);
        await probe.WaitUntil(() =>
        {
            probe.Ping.Emit("sync");
            return line.IsCompleted;
        }, "The listener did not subscribe.");
        probe.Ping.Emit("done");
        var value = await line;
        while (value != "done")
        {
            Assert.IsNotNull(value);
            value = await ReadLine(client);
        }
    }

    private static async Task Fill(Probe probe, List<NamedPipeClientStream> held, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var client = await Connect(probe.Pipe);
            held.Add(client);
            await Subscribe(probe, client);
        }
    }

    private static async Task<string?> ReadLine(Stream stream)
    {
        var line = new List<byte>();
        var one = new byte[1];
        while (true)
        {
            var n = await stream.ReadAsync(one, CurrentTest.Cancellation).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(10), CurrentTest.Cancellation);
            if (n == 0)
                return null;
            if (one[0] != (byte)'\n')
            {
                line.Add(one[0]);
                continue;
            }

            var response = JsonSerializer.Deserialize(line.ToArray(), IpcJson.Default.IpcResponse);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.Ok);
            return response.Value;
        }
    }

    private static string PipeName()
    {
        return "winshell-test-" + Guid.NewGuid().ToString("N");
    }

    private static async Task<NamedPipeClientStream> Connect(string name)
    {
        var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(3000);
        return client;
    }

    private static async Task Ask(string name, TaskCompletionSource<string> answered)
    {
        try
        {
            await using var client = await Connect(name);
            await client.WriteAsync("{\"op\":\"show\"}\n"u8.ToArray());
            using var reader = new StreamReader(client, Encoding.UTF8, false, leaveOpen: true);
            if (await reader.ReadLineAsync(CurrentTest.Cancellation) is { } line)
                answered.TrySetResult(line);
        }
        catch (Exception e) when (e is IOException or TimeoutException or OperationCanceledException)
        {
        }
    }

    private static async Task<NamedPipeClientStream> ConnectWhenFree(string name)
    {
        NamedPipeClientStream? client = null;
        await Wait.UntilAsync(() => client is not null, "The pipe never had a free instance.", () =>
        {
            var attempt = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                attempt.Connect(200);
                client = attempt;
            }
            catch (TimeoutException)
            {
                attempt.Dispose();
            }
        });
        return client!;
    }

    private static async Task<List<string>> ReadLines(Stream stream, int count, byte already)
    {
        var pending = new List<byte> { already };
        var lines = new List<string>();
        var buf = new byte[8192];
        while (lines.Count < count)
        {
            int n;
            try
            {
                n = await stream.ReadAsync(buf, CurrentTest.Cancellation);
            }
            catch (OperationCanceledException)
            {
                Assert.Fail($"The listener delivered {lines.Count} lines, not {count}.");
                throw;
            }

            Assert.AreNotEqual(0, n);
            for (var i = 0; i < n; i++)
                if (buf[i] == (byte)'\n')
                {
                    lines.Add(Encoding.UTF8.GetString(pending.ToArray()));
                    pending.Clear();
                    if (lines.Count == count)
                        break;
                }
                else if (buf[i] != (byte)'\r')
                {
                    pending.Add(buf[i]);
                }
        }

        return lines;
    }

    private sealed class Probe : IDisposable
    {
        public Probe()
        {
            var control = new FakeInstanceControl();
            Ping = control.Ipc.Signal("probe", "ping");
            Loop = new ManualEventLoop();
            Pipe = "winshell-test-" + Guid.NewGuid().ToString("N");
            Server = new IpcServer(IpcServer.OpenFirst(Pipe), Pipe, Loop, Log.Default, control);
        }

        private ManualEventLoop Loop { get; }

        private IpcServer Server { get; }

        public string Pipe { get; }

        public IpcSignal Ping { get; }

        public Task WaitUntil(Func<bool> ready, string why)
        {
            return Wait.UntilAsync(ready, why, Loop.Process);
        }

        public void Dispose()
        {
            Server.Dispose();
            Loop.Process();
        }
    }
}
