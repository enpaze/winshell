using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text.Json;
using ThreadState = System.Threading.ThreadState;

namespace Winshell.Services.Tests;

/// <summary>
/// Becoming an instance in this process: a duplicate, a detach that cannot start, a pipe that cannot open, and a
/// clean start. The local folder is <c>WINSHELL_LOCAL_DIR</c>. The mutex and the pipes are the real ones.
/// </summary>
[Collection(InstanceRunCollection.Name)]
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

    /// <summary>
    /// The start's exit code, and the lines of standard error, where a failed start says why. Other tests in this
    /// process may write there too, so a test looks for its line among them.
    /// </summary>
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

    [Fact]
    public async Task A_duplicate_exits_before_the_shell_exists()
    {
        // A mutex is owned by the thread that waited, and a second wait on that thread succeeds. The shell that
        // already has this id is another thread, which is what a second process is too.
        var shellId = ShellId();
        using var taken = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holder = new Thread(() =>
            {
                using var mutex = new Mutex(false, DuplicateHold.ShellMutexName(ShellLocations.Current, shellId));
                mutex.WaitOne();
                // Joined before this method disposes the handles.
                taken.Set();
                release.Wait();
                mutex.ReleaseMutex();
            })
        { IsBackground = true };
        holder.Start();
        taken.Wait(TestContext.Current.CancellationToken);
        var hosted = false;

        try
        {
            var result = Start(Launch(shellId, Program, ["--no-duplicate"]), _ => hosted = true);

            Assert.False(hosted);
            Assert.Equal(0, result.Code);
        }
        finally
        {
            release.Set();
            await Join(holder);
        }
    }

    [Fact]
    public void A_detach_that_cannot_start_reports_why()
    {
        var program = Path.Combine(ShellLocations.Current.LocalRoot, "missing-shell.exe");
        var hosted = false;

        var result = Start(Launch(ShellId(), program, ["-d"]), _ => hosted = true);

        Assert.False(hosted);
        Assert.Equal(InstanceStart.CouldNotStart, result.Code);
        Assert.Contains(result.Error,
            line => line.StartsWith($"Could not start \"{program}\":", StringComparison.Ordinal));
    }

    [Fact]
    public void A_handover_from_a_process_that_has_gone_still_starts()
    {
        using var gone = Process.Start(new ProcessStartInfo(ComSpec(), "/d /c exit")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        // HasExited is read before this method disposes the process.
        Wait.Until(() => gone.HasExited, "the previous process to exit");
        var hosted = false;

        var result = Start(Launch(ShellId(), Program,
            ["--after", gone.Id.ToString(CultureInfo.InvariantCulture)]), session =>
        {
            hosted = true;
            session.Serve(new EventQueue(), new FakeInstanceControl());
        });

        Assert.True(hosted);
        Assert.Equal(0, result.Code);
    }

    [Fact]
    public async Task A_handover_runs_only_after_the_replaced_process_exits()
    {
        using var replaced = Process.Start(new ProcessStartInfo(ComSpec(), "/d /c pause")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            // The pause prompt must not prefix a JSON message on xUnit's protocol stdout.
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
                    // The start is joined before this method disposes the process and the event.
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
            Assert.False(replaced.HasExited);
            Assert.False(started.IsSet);

            replaced.StandardInput.Close();
            // Both are read before this method disposes the process and the event.
            await Wait.UntilAsync(() => replaced.HasExited, "the replaced process to exit");
            await Wait.UntilAsync(() => started.IsSet, "the shell to start");
            await Join(thread);

            Assert.Null(error);
            Assert.Equal(0, code);
        }
        finally
        {
            try
            {
                if (!replaced.HasExited)
                    replaced.Kill(true);
            }
            // Kill throws when the process has already exited between the check and the call.
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
        return Task.Run(thread.Join).WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void A_pipe_that_cannot_open_is_abandoned()
    {
        var held = new List<NamedPipeServerStream>();
        string? id = null;
        var hosted = false;
        try
        {
            // Prepare runs after the instance exists and before the pipe opens, which is also when a shell wires
            // crash release. The names it will try are held here, so this start cannot take one.
            var result = Start(Launch(ShellId(), Program, [], instance =>
            {
                id = instance.Id;
                foreach (var name in IpcServer.Candidates(instance.Pipe))
                    held.Add(IpcServer.OpenFirst(name));
            }), _ => hosted = true);

            Assert.False(hosted);
            Assert.Equal(InstanceStart.CouldNotStart, result.Code);
            Assert.Contains("The control pipe is already taken.", result.Error);
            Assert.False(Directory.Exists(ShellLocations.Current.Instances.DirOf(id!)));
        }
        finally
        {
            foreach (var pipe in held)
                pipe.Dispose();
        }
    }

    [Fact]
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

        Assert.Equal(0, result.Code);
        Assert.NotNull(published);
        Assert.Equal(shellId, published.ShellId);
        Assert.Equal(Environment.ProcessId, published.Pid);
        Assert.Null(published.ExitedAt);
        var closed = Read(id!);
        Assert.NotNull(closed.ExitedAt);
        Assert.StartsWith("winshell-", closed.Pipe, StringComparison.Ordinal);
    }

    private static string ComSpec()
    {
        return Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
    }
}
