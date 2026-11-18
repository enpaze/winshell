using System.Globalization;
using System.IO.Pipes;
using System.Text;
using Winshell.Winland;

namespace Winshell.Services.Tests;

public sealed class WinlandArrivalTests
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private const string Monitors = """
                                    [{"id":0,"name":"\\\\.\\DISPLAY1","description":"Main","x":0,"y":0,"width":1920,"height":1080,"scale":1,"focused":true,
                                      "activeWorkspace":{"id":1,"name":"1"}}]
                                    """;

    private const string Workspaces = """
                                      [{"id":1,"name":"1","monitor":"\\\\.\\DISPLAY1","monitorID":0,"hasfullscreen":false}]
                                      """;

    [Fact]
    public void Workspaces_show_within_a_second_of_winland_starting_and_clear_when_it_stops()
    {
        var name = PipeName();
        var loop = new ManualEventLoop();
        using var service = new WinlandService(loop, Log.Default, new WinlandBackend(TimeProvider.System, [name]));
        Wait.ForNothingToHappen(TimeSpan.FromMilliseconds(750));
        loop.Process();
        Assert.Equal(0, service.ProcessId);
        Assert.Empty(service.Workspaces);

        var opened = TimeProvider.System.GetTimestamp();
        using (var peer = Peer.Start(name))
        {
            Wait.Until(Shown, "workspaces to show", Second, loop.Process);
            var shownAfter = TimeProvider.System.GetElapsedTime(opened);
            TestContext.Current.TestOutputHelper?.WriteLine($"shown after {shownAfter.TotalMilliseconds:0} ms");
            Assert.True(shownAfter < Second, $"workspaces showed after {shownAfter.TotalMilliseconds:0} ms");
            Assert.Contains(service.Monitors, monitor => monitor.Name == @"\\.\DISPLAY1");
            Assert.True(service.ProcessId > 0);

            peer.Stop();
            var closed = TimeProvider.System.GetTimestamp();
            Wait.Until(Cleared, "workspaces to clear", Second, loop.Process);
            var clearedAfter = TimeProvider.System.GetElapsedTime(closed);
            TestContext.Current.TestOutputHelper?.WriteLine($"cleared after {clearedAfter.TotalMilliseconds:0} ms");
            Assert.True(clearedAfter < Second, $"workspaces cleared after {clearedAfter.TotalMilliseconds:0} ms");
        }

        return;

        bool Shown()
        {
            return service.Workspaces.Count > 0;
        }

        bool Cleared()
        {
            return service.Workspaces.Count == 0 && service.Monitors.Count == 0 && service.ProcessId == 0;
        }
    }

    [Fact]
    public void Workspaces_show_within_a_second_when_winland_is_already_running()
    {
        var name = PipeName();
        using var peer = Peer.Start(name);
        var loop = new ManualEventLoop();
        var opened = TimeProvider.System.GetTimestamp();
        using var service = new WinlandService(loop, Log.Default, new WinlandBackend(TimeProvider.System, [name]));
        Wait.Until(() => service.Workspaces.Count > 0, "workspaces to show", Second, loop.Process);
        var shownAfter = TimeProvider.System.GetElapsedTime(opened);
        TestContext.Current.TestOutputHelper?.WriteLine($"shown after {shownAfter.TotalMilliseconds:0} ms");
        Assert.True(shownAfter < Second, $"workspaces showed after {shownAfter.TotalMilliseconds:0} ms");
        Assert.Contains(service.Monitors, monitor => monitor.Name == @"\\.\DISPLAY1");
    }

    private static string PipeName()
    {
        return "winshell-test-" + Guid.NewGuid().ToString("N");
    }

    private sealed class Peer : IDisposable
    {
        private readonly CancellationTokenSource _cancel = new();
        private readonly List<NamedPipeServerStream> _pipes = [];
        private readonly Lock _gate = new();
        private int _listening;

        private Peer(string name)
        {
            Name = name;
        }

        private string Name { get; }

        public static Peer Start(string name)
        {
            var peer = new Peer(name);
            for (var i = 0; i < 8; i++)
                _ = Task.Run(() => peer.Accept(peer._cancel.Token));
            Wait.Until(() => Volatile.Read(ref peer._listening) >= 4, "the pipe to listen");
            return peer;
        }

        public void Stop()
        {
            _cancel.Cancel();
            DisposePipes();
        }

        public void Dispose()
        {
            Stop();
        }

        private async Task Accept(CancellationToken cancel)
        {
            while (!cancel.IsCancellationRequested)
            {
                var pipe = Create();
                var accepted = false;
                try
                {
                    Interlocked.Increment(ref _listening);
                    await pipe.WaitForConnectionAsync(cancel).ConfigureAwait(false);
                    accepted = true;
                }
                catch (Exception e) when (e is OperationCanceledException or IOException or ObjectDisposedException
                                              or UnauthorizedAccessException)
                {
                    if (cancel.IsCancellationRequested)
                        return;
                }
                finally
                {
                    Interlocked.Decrement(ref _listening);
                    if (!accepted)
                        pipe.Dispose();
                }

                if (!accepted)
                    continue;
                try
                {
                    await Answer(pipe, cancel).ConfigureAwait(false);
                }
                catch (Exception e) when (e is OperationCanceledException or IOException or ObjectDisposedException
                                              or UnauthorizedAccessException)
                {
                    if (cancel.IsCancellationRequested)
                        return;
                }
            }
        }

        private async Task Answer(NamedPipeServerStream pipe, CancellationToken cancel)
        {
            var hold = false;
            try
            {
                using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false,
                    bufferSize: 256, leaveOpen: true);
                var command = await reader.ReadLineAsync(cancel).ConfigureAwait(false);
                hold = command == "events";
                if (hold)
                    await Task.Delay(Timeout.Infinite, cancel).ConfigureAwait(false);
                else if (command is not null && Reply(command) is { } body)
                    await Write(pipe, body, cancel).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (!hold || cancel.IsCancellationRequested)
                    pipe.Dispose();
            }
        }

        private static string? Reply(string command)
        {
            return command switch
            {
                "j/monitors" => Monitors,
                "j/workspaces" => Workspaces,
                "j/clients" => "[]",
                "j/activewindow" => "{}",
                "j/files" => """{"config":"C:\\winland.conf","log":"C:\\winland.log"}""",
                _ => null,
            };
        }

        private static async Task Write(Stream pipe, string body, CancellationToken cancel)
        {
            var payload = Encoding.UTF8.GetBytes(body);
            var header = Encoding.UTF8.GetBytes($"#{payload.Length.ToString(CultureInfo.InvariantCulture)}\n");
            await pipe.WriteAsync(header, cancel).ConfigureAwait(false);
            await pipe.WriteAsync(payload, cancel).ConfigureAwait(false);
            await pipe.FlushAsync(cancel).ConfigureAwait(false);
        }

        private NamedPipeServerStream Create()
        {
            var pipe = new NamedPipeServerStream(Name, PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            lock (_gate)
                _pipes.Add(pipe);
            return pipe;
        }

        private void DisposePipes()
        {
            NamedPipeServerStream[] pipes;
            lock (_gate)
                pipes = _pipes.ToArray();
            foreach (var pipe in pipes)
                pipe.Dispose();
        }
    }
}
