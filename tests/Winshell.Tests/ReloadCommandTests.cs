using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Winshell.Tests;

[TestClass]
public sealed partial class ReloadCommandTests : IDisposable
{
    private const int BuildTimeout = 600_000;

    private readonly string _dir = TempFolder.NewPath();
    private readonly string _local;
    private readonly string _config;
    private readonly List<Process> _started = [];

    private sealed partial class OwnProbe : IDisposable
    {
        private readonly string _dir = TempFolder.NewPath();

        public OwnProbe()
        {
            _ = ProbeShell.Program;
            Program = ProbeShell.Build(Folder);
        }

        public string Folder => Path.Combine(_dir, "probe");

        public string Program { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static OwnProbe _probe = null!;

    [ClassInitialize]
    public static void Build(TestContext context)
    {
        _probe = new OwnProbe();
    }

    [ClassCleanup]
    public static void Remove()
    {
        _probe.Dispose();
    }

    public ReloadCommandTests()
    {
        _local = Path.Combine(_dir, "local");
        _config = Path.Combine(_dir, "config");
        Directory.CreateDirectory(_local);
        Directory.CreateDirectory(_config);
    }

    [TestMethod, Timeout(BuildTimeout)]
    [TestCategory("Window")]
    public void A_build_that_fails_leaves_the_instance_running()
    {
        CurrentTest.Cancellation.ThrowIfCancellationRequested();
        var source = Path.Combine(_probe.Folder, "Program.cs");
        var original = File.ReadAllText(source);
        var process = StartProbe();
        try
        {
            File.WriteAllText(source, original + "\nthis is not C#\n");
            var result = Run("reload", "--pid", process.Id.ToString(CultureInfo.InvariantCulture));

            result.AssertExitCode(1);
            Assert.AreEqual("", result.Output);
            Assert.Contains("error ", result.Error, StringComparison.Ordinal);
            Assert.DoesNotContain("did not reload", result.Error, StringComparison.Ordinal);
            Assert.DoesNotContain("could not show the notice", result.Error, StringComparison.Ordinal);
            Assert.IsFalse(process.HasExited);
            Assert.Contains(row => row.GetProperty("pid").GetInt32() == process.Id, Listed());
        }
        finally
        {
            File.WriteAllText(source, original);
        }
    }

    [TestMethod, Timeout(BuildTimeout)]
    public void A_second_reload_runs_the_second_edit()
    {
        CurrentTest.Cancellation.ThrowIfCancellationRequested();
        var project = Path.Combine(_probe.Folder, "Probe.csproj");
        var original = File.ReadAllText(project);
        var process = StartProbe();
        try
        {
            var first = ReloadTo(process, original, "first");
            var second = ReloadTo(first, original, "second");

            Assert.AreNotEqual(first.Id, second.Id);
        }
        finally
        {
            File.WriteAllText(project, original);
        }
    }

    [TestMethod, Timeout(BuildTimeout)]
    public void A_shell_that_restarts_itself_hands_over_to_its_own_program_without_a_build()
    {
        CurrentTest.Cancellation.ThrowIfCancellationRequested();
        var project = Path.Combine(_probe.Folder, "Probe.csproj");
        var original = File.ReadAllText(project);
        var output = Path.GetDirectoryName(_probe.Program)!;
        var built = Directory.GetFiles(output, "*", SearchOption.AllDirectories).ToDictionary(file => file, File.GetLastWriteTimeUtc);
        var process = StartProbe();
        var runningName = new Instances(Path.Combine(_local, "run")).Running().Single(info => info.Pid == process.Id).ShellName;
        try
        {
            File.WriteAllText(project, Named(original, "rebuilt"));

            Run("ipc", "--pid", process.Id.ToString(CultureInfo.InvariantCulture), "call", "self", "restart")
                .AssertExitCode(0);

            var next = NextPid(process.Id, null);
            _started.Add(Process.GetProcessById(next));
            Wait.Until(() => process.HasExited, "the old process to exit");
            var instance = new Instances(Path.Combine(_local, "run")).Running().Single(info => info.Pid == next);
            Assert.AreEqual(_probe.Program, instance.Program, StringComparer.OrdinalIgnoreCase);
            Assert.AreEqual(runningName, instance.ShellName);
            Assert.AreEquivalent(built, Directory.GetFiles(output, "*", SearchOption.AllDirectories).ToDictionary(file => file, File.GetLastWriteTimeUtc));
            Assert.IsFalse(ExitIntentEvents.IsSupervised(next));
        }
        finally
        {
            File.WriteAllText(project, original);
        }
    }

    [TestMethod, Timeout(BuildTimeout)]
    public void A_reload_under_winshell_run_stays_supervised()
    {
        CurrentTest.Cancellation.ThrowIfCancellationRequested();
        var project = Path.Combine(_probe.Folder, "Probe.csproj");
        var original = File.ReadAllText(project);
        var run = Supervise();
        try
        {
            var first = NextPid(0, null);
            File.WriteAllText(project, Named(original, "reloaded"));

            Reload(first);

            var second = NextPid(first, "reloaded");
            using (var crashed = Process.GetProcessById(second))
            {
                crashed.Kill();
                crashed.WaitForExit();
            }

            NextPid(second, "reloaded");
            Assert.IsFalse(run.HasExited);
        }
        finally
        {
            File.WriteAllText(project, original);
        }
    }

    [TestMethod, Timeout(BuildTimeout)]
    public void A_reload_under_winshell_run_ends_a_shell_that_hangs_on_its_way_out()
    {
        CurrentTest.Cancellation.ThrowIfCancellationRequested();
        var hang = Path.Combine(_config, ProbeShell.HangMarker);
        File.WriteAllText(hang, "");
        var run = Supervise();
        var shell = NextPid(0, null);
        var pidFile = Path.Combine(_config, "child.pid");
        Wait.Until(() => File.Exists(pidFile), "the shell's program to start");
        File.Delete(hang);
        using var program = Process.GetProcessById(int.Parse(File.ReadAllText(pidFile), CultureInfo.InvariantCulture));
        using var hung = Process.GetProcessById(shell);
        try
        {
            Reload(shell);

            Assert.IsTrue(hung.HasExited);
            NextPid(shell, null);
            Assert.IsFalse(run.HasExited);
        }
        finally
        {
            program.Kill();
            program.WaitForExit(5000);
        }
    }

    [TestMethod]
    public void A_reload_under_winshell_run_without_its_project_is_refused()
    {
        var run = Supervise();
        var shell = NextPid(0, null);
        var projectFile = Directory.GetFiles(_probe.Folder, "*.csproj").Single();
        File.Move(projectFile, projectFile + ".gone");
        try
        {
            var result = Run("reload", "--pid", shell.ToString(CultureInfo.InvariantCulture));

            var id = Listed().Single(row => row.GetProperty("pid").GetInt32() == shell).GetProperty("id").GetString();
            result.AssertExitCode(1);
            Assert.AreEqual("", result.Output);
            Assert.AreEqual(
                $"Could not find the project that builds instance {id}, so it was not reloaded.{Environment.NewLine}",
                result.Error);
            Assert.IsFalse(run.HasExited);
        }
        finally
        {
            File.Move(projectFile + ".gone", projectFile);
        }
    }

    private Process Supervise()
    {
        var info = WinshellCli.StartInfo(["run", "--no-build", "-p", _probe.Folder]);
        foreach (var (name, value) in IsolatedFolders.Of(_config, _local))
            info.Environment[name] = value;
        var run = ChildProcess.Start(info, null);
        _started.Add(run);
        return run;
    }

    private int NextPid(int previous, string? shellName)
    {
        var pid = 0;
        Wait.Until(
            () =>
            {
                pid = new Instances(Path.Combine(_local, "run")).Running()
                    .Where(info => info.Pid != previous && (shellName is null || info.ShellName == shellName))
                    .Select(info => info.Pid)
                    .FirstOrDefault();
                return pid != 0;
            },
            $"a shell after {previous} in winshell list");
        return pid;
    }

    private static string Named(string project, string shellName)
    {
        var named = project.Replace("<ShellName>probe</ShellName>", $"<ShellName>{shellName}</ShellName>",
            StringComparison.Ordinal);
        Assert.AreNotEqual(project, named);
        return named;
    }

    private Process ReloadTo(Process running, string original, string shellName)
    {
        File.WriteAllText(Path.Combine(_probe.Folder, "Probe.csproj"), Named(original, shellName));
        var result = Reload(running.Id);
        result.AssertNoError();
        Assert.StartsWith("Reloaded ", result.Output, StringComparison.Ordinal);
        Wait.Until(() => running.HasExited, "the old process to exit");
        var pid = 0;
        Wait.Until(
            () =>
            {
                pid = new Instances(Path.Combine(_local, "run")).Running().Where(info => info.ShellName == shellName)
                    .Select(info => info.Pid).FirstOrDefault();
                return pid != 0;
            },
            $"the process that runs the {shellName} edit");
        var next = Process.GetProcessById(pid);
        _started.Add(next);
        return next;
    }

    private ChildProcess Reload(int pid)
    {
        var result = Run("reload", "--pid", pid.ToString(CultureInfo.InvariantCulture));
        result.AssertExitCode(0);
        return result;
    }

    private Process StartProbe()
    {
        var process = ProbeShell.Start(_config, _local, program: _probe.Program);
        _started.Add(process);
        return process;
    }

    private List<JsonElement> Listed()
    {
        var result = Run("list", "--json");
        result.AssertExitCode(0);
        using var json = JsonDocument.Parse(result.Output);
        return json.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private ChildProcess Run(params string[] args)
    {
#pragma warning disable CsWinRT1030
        return WinshellCli.Run(args, IsolatedFolders.Of(_config, _local));
#pragma warning restore CsWinRT1030
    }

    public void Dispose()
    {
        List<JsonElement> rows = [];
        try
        {
            rows = Listed();
        }
        catch (UnitTestAssertException)
        {
        }

        foreach (var row in rows)
            try
            {
                Run("stop", "--force", "--pid",
                    row.GetProperty("pid").GetInt32().ToString(CultureInfo.InvariantCulture));
            }
            catch (UnitTestAssertException)
            {
            }

        foreach (var process in _started)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(true);
                }
                catch (Exception)
                {
                }

                process.WaitForExit();
            }

            process.Dispose();
        }

        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }
}
