using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text.Json;
using ThreadState = System.Threading.ThreadState;

namespace Winshell.Services.Tests;

[TestClass]
[DoNotParallelize]
public sealed class InstanceStartTests
{
    private const string Program = @"C:\shells\mine.exe";

    private static string ShellId()
    {
        return "start-" + Guid.NewGuid().ToString("N");
    }

    private static InstanceLaunch Launch(string shellId, string program, IReadOnlyList<string> args,
        Action<ShellInstance>? prepare = null)
    {
        return new InstanceLaunch(program, args, shellId, null, null, null, new Log(), prepare);
    }

    private static (int Code, string[] Error) Start(InstanceLaunch launch, Action<InstanceSession> host)
    {
        var error = new StringWriter();
        var previous = Console.Error;
        Console.SetError(error);
        int code;
        try
        {
            code = InstanceStart.Run(launch, host);
        }
        finally
        {
            Console.SetError(previous);
        }

        return (code, error.ToString().Split(Environment.NewLine));
    }

    private static InstanceInfo Read(string id)
    {
        var file = Path.Combine(ShellLocations.Current.Instances.DirOf(id), "instance.json");
        return JsonSerializer.Deserialize(File.ReadAllText(file), InstanceJson.Default.InstanceInfo)!;
    }

    [TestMethod]
    public async Task A_duplicate_exits_before_the_shell_exists()
    {
        var shellId = ShellId();
        using var taken = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holder = new Thread(() =>
            {
                using var mutex = new Mutex(false, HeldMutex.NoDuplicateName(ShellLocations.Current, shellId));
                mutex.WaitOne();
                taken.Set();
                release.Wait(CurrentTest.Cancellation);
                mutex.ReleaseMutex();
            })
        { IsBackground = true };
        holder.Start();
        taken.Wait(CurrentTest.Cancellation);
        var hosted = false;

        try
        {
            var result = Start(Launch(shellId, Program, ["--no-duplicate"]), _ => hosted = true);

            Assert.IsFalse(hosted);
            Assert.AreEqual(0, result.Code);
        }
        finally
        {
            release.Set();
            await Join(holder);
        }
    }

    [TestMethod]
    public void A_detach_that_cannot_start_reports_why()
    {
        var program = Path.Combine(ShellLocations.Current.LocalRoot, "missing-shell.exe");
        var hosted = false;

        var result = Start(Launch(ShellId(), program, ["-d"]), _ => hosted = true);

        Assert.IsFalse(hosted);
        Assert.AreEqual(InstanceStart.CouldNotStart, result.Code);
        Assert.Contains(line => line.StartsWith($"Could not start \"{program}\":", StringComparison.Ordinal),
            result.Error);
    }

    [TestMethod]
    public void A_handover_from_a_process_that_has_gone_still_starts()
    {
        using var gone = Process.Start(new ProcessStartInfo(ComSpec(), "/d /c exit")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        Wait.Until(() => gone.HasExited, "the previous process to exit");
        var hosted = false;

        var result = Start(Launch(ShellId(), Program,
            ["--after", gone.Id.ToString(CultureInfo.InvariantCulture)]), session =>
        {
            hosted = true;
            session.Serve(new EventQueue(), new FakeInstanceControl());
        });

        Assert.IsTrue(hosted);
        Assert.AreEqual(0, result.Code);
    }

    [TestMethod]
    public async Task A_handover_runs_only_after_the_replaced_process_exits()
    {
        using var replaced = Process.Start(new ProcessStartInfo(ComSpec(), "/d /c pause")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        })!;
        using var started = new ManualResetEventSlim();
        var code = -1;
        Exception? error = null;
        var thread = new Thread(() =>
            {
                try
                {
                    code = Start(Launch(ShellId(), Program,
                        ["--after", replaced.Id.ToString(CultureInfo.InvariantCulture)]), session =>
                    {
                        started.Set();
                        session.Serve(new EventQueue(), new FakeInstanceControl());
                    }).Code;
                }
                catch (Exception e)
                {
                    error = e;
                }
            })
        { IsBackground = true };

        try
        {
            thread.Start();
            Wait.ForNothingToHappen(TimeSpan.FromMilliseconds(200));
            Assert.IsFalse(replaced.HasExited);
            Assert.IsFalse(started.IsSet);

            replaced.StandardInput.Close();
            await Wait.UntilAsync(() => replaced.HasExited, "the replaced process to exit");
            await Wait.UntilAsync(() => started.IsSet, "the shell to start");
            await Join(thread);

            Assert.IsNull(error);
            Assert.AreEqual(0, code);
        }
        finally
        {
            try
            {
                if (!replaced.HasExited)
                    replaced.Kill(true);
            }
            catch (Exception)
            {
            }

            await Join(thread);
        }
    }

    private static Task Join(Thread thread)
    {
        if (thread.ThreadState == ThreadState.Unstarted)
            return Task.CompletedTask;
        return Task.Run(thread.Join).WaitAsync(CurrentTest.Cancellation);
    }

    [TestMethod]
    public void A_pipe_that_cannot_open_is_abandoned()
    {
        var held = new List<NamedPipeServerStream>();
        string? id = null;
        var hosted = false;
        try
        {
            var result = Start(Launch(ShellId(), Program, [], instance =>
            {
                id = instance.Id;
                foreach (var name in IpcServer.Candidates(instance.Pipe))
                    held.Add(IpcServer.OpenFirst(name));
            }), _ => hosted = true);

            Assert.IsFalse(hosted);
            Assert.AreEqual(InstanceStart.CouldNotStart, result.Code);
            Assert.Contains("The control pipe is already taken.", result.Error);
            Assert.IsFalse(Directory.Exists(ShellLocations.Current.Instances.DirOf(id!)));
        }
        finally
        {
            foreach (var pipe in held)
                pipe.Dispose();
        }
    }

    [TestMethod]
    public void A_clean_start_publishes_the_instance_and_then_closes_it()
    {
        var shellId = ShellId();
        string? id = null;

        InstanceInfo? published = null;
        var result = Start(Launch(shellId, Program, []), session =>
        {
            session.Serve(new EventQueue(), new FakeInstanceControl());
            id = session.Instance.Id;
            published = Read(id);
        });

        Assert.AreEqual(0, result.Code);
        Assert.IsNotNull(published);
        Assert.AreEqual(shellId, published.ShellId);
        Assert.AreEqual(Environment.ProcessId, published.Pid);
        Assert.IsNull(published.ExitedAt);
        var closed = Read(id!);
        Assert.IsNotNull(closed.ExitedAt);
        Assert.StartsWith("winshell-", closed.Pipe, StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_host_that_throws_records_the_crash_and_then_closes_the_log()
    {
        var log = new Log();
        ShellInstance? instance = null;
        var error = new InvalidOperationException("SENTINEL host");

        var thrown = Assert.ThrowsExactly<InvalidOperationException>(() =>
            Start(new InstanceLaunch(Program, [], ShellId(), null, null, null, log), session =>
            {
                instance = session.Instance;
                throw error;
            }));

        Assert.AreSame(error, thrown);
        var text = File.ReadAllText(instance!.LogPath);
        Assert.Contains("Winshell crashed", text, StringComparison.Ordinal);
        Assert.Contains("SENTINEL host", text, StringComparison.Ordinal);

        var stderr = new StringWriter();
        var previous = Console.Error;
        Console.SetError(stderr);
        try
        {
            log.Error("shell", "AFTER-CLOSE");
        }
        finally
        {
            Console.SetError(previous);
        }

        Assert.DoesNotContain("AFTER-CLOSE", File.ReadAllText(instance.LogPath), StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_running_shell_quits_before_the_next_one_starts()
    {
        using var other = Hold("running", out var control, out var loop, out var server, out var id);
        using var asked = new EventWaitHandle(false, EventResetMode.ManualReset,
            ExitIntentEvents.Name(ExitIntent.Stop, other.Id));
        using var stop = new CancellationTokenSource();
        var pump = Pump(loop, stop.Token);
        var hosted = false;
        try
        {
            control.OnYield = () =>
            {
                try
                {
                    if (!other.HasExited)
                        other.Kill(true);
                }
                catch (Exception e) when (e is InvalidOperationException or Win32Exception)
                {
                }
            };

            var result = Start(Launch(ShellId(), Program, ["--no-duplicate"]), _ => hosted = other.HasExited);

            Assert.IsTrue(hosted);
            Assert.AreEqual(0, result.Code);
            Assert.IsTrue(other.HasExited);
            Assert.AreEqual(1, control.Yields);
            Assert.IsTrue(asked.WaitOne(0));
        }
        finally
        {
            stop.Cancel();
            pump.Join(TimeSpan.FromSeconds(2));
            server.Dispose();
            Forget(id);
        }
    }

    [TestMethod]
    public void A_shell_that_does_not_quit_stays_and_the_start_says_why()
    {
        using var other = Hold("running", out var control, out var loop, out var server, out var id);
        using var asked = new EventWaitHandle(false, EventResetMode.ManualReset,
            ExitIntentEvents.Name(ExitIntent.Stop, other.Id));
        using var stop = new CancellationTokenSource();
        var pump = Pump(loop, stop.Token);
        var hosted = false;
        try
        {
            var result = Start(Launch(ShellId(), Program, []), _ => hosted = true);

            Assert.IsFalse(hosted);
            Assert.IsFalse(other.HasExited);
            Assert.AreEqual(InstanceStart.CouldNotStart, result.Code);
            Assert.IsFalse(asked.WaitOne(0));
            Assert.Contains(line => line.Contains("running", StringComparison.Ordinal) &&
                                                   line.Contains("did not quit", StringComparison.Ordinal), result.Error);
        }
        finally
        {
            stop.Cancel();
            pump.Join(TimeSpan.FromSeconds(2));
            server.Dispose();
            if (!other.HasExited)
                other.Kill(true);
            Forget(id);
        }
    }

    private static Process Hold(string shellId, out FakeInstanceControl control, out EventQueue loop, out IpcServer server,
        out string id)
    {
        var process = Process.Start(new ProcessStartInfo(ComSpec(), "/d /c ping -n 30 127.0.0.1")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        control = new FakeInstanceControl();
        var pipe = "winshell-test-" + Guid.NewGuid().ToString("N");
        loop = new EventQueue();
        server = new IpcServer(IpcServer.OpenFirst(pipe), pipe, loop, Log.Default, control);
        id = Instances.NewId();
        var locations = ShellLocations.Current;
        locations.Instances.Write(new InstanceInfo(id, process.Id, shellId, process.StartInfo.FileName!,
            DateTime.Now, pipe, Path.Combine(locations.Instances.DirOf(id), "log.txt"),
            process.StartTime.ToUniversalTime().Ticks, ProcessName: process.ProcessName));
        return process;
    }

    private static Thread Pump(EventQueue loop, CancellationToken stop)
    {
        var pump = new Thread(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                loop.RunDue();
                Thread.Sleep(10);
            }
        })
        { IsBackground = true };
        pump.Start();
        return pump;
    }

    private static void Forget(string id)
    {
        try
        {
            Directory.Delete(ShellLocations.Current.Instances.DirOf(id), true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string ComSpec()
    {
        return Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
    }
}
