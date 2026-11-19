using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Winshell.Tests;

[Collection(ExplorerTaskbarCollection.Name)]
public sealed partial class SupervisionTests : IDisposable, IClassFixture<ProbeBuild>
{
    private readonly string _dir = TempFolder.NewPath();
    private readonly List<Process> _started = [];
    private readonly StringBuilder _printed = new();
    private readonly uint _appBarState = AppBars.State();
    private bool? _taskbarHidden;

    public SupervisionTests()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "config"));
        Directory.CreateDirectory(Path.Combine(_dir, "local"));
    }

    private (string, string)[] Folders => IsolatedFolders.Under(_dir);

    [GeneratedRegex(@"^Log: (?<path>.+), last \d+ lines\r?$", RegexOptions.Multiline)]
    private static partial Regex LogLine();

    private static string Project =>
        Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(ProbeShell.Program)))!;

    [Fact]
    public void List_log_ipc_and_stop_reach_the_program()
    {
        var run = Supervise();
        using var process = Process.GetProcessById(NextShell(0));
        _ = process.SafeHandle;
        var pid = process.Id.ToString(CultureInfo.InvariantCulture);

        var row = Assert.Single(Rows());
        Assert.Equal("probe", row.GetProperty("shellName").GetString());
        Assert.Equal(ProbeShell.Program, row.GetProperty("program").GetString(), true);

        var log = Cli("log", "--pid", pid);
        log.AssertExitCode(0);
        Assert.Contains("Shell name: \"probe\"", log.Output, StringComparison.Ordinal);

        var show = Cli("ipc", "--pid", pid, "show");
        show.AssertExitCode(0);
        show.AssertNoError();
        var call = Cli("ipc", "--pid", pid, "call", "nothing", "open");
        call.AssertExitCode(1);
        Assert.Equal("Target not found: nothing.", call.Error.Trim());

        var stop = Cli("stop", "--pid", pid);
        stop.AssertExitCode(0);
        stop.AssertNoError();
        Assert.StartsWith("Stopped ", stop.Output, StringComparison.Ordinal);
        Assert.True(process.HasExited);
        ChildProcess.Exited("The probe", process, _printed).AssertExitCode(0);
        Assert.Empty(Listed());
        Wait.Until(() => run.HasExited, "winshell run to end with its shell");
        ChildProcess.Exited("winshell run", run, _printed).AssertExitCode(0);

        var stopped = Cli("log");
        stopped.AssertExitCode(0);
        Assert.Contains("Showing the log of stopped instance", stopped.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_crash_as_the_shell_starts_writes_one_report_and_leaves_no_running_instance()
    {
        File.WriteAllText(Path.Combine(_dir, "config", ProbeShell.CrashMarker), "");

        var result = Cli("run", "--no-build", "-p", Project);

        result.AssertExitCode(1);
        var report = File.ReadAllText(Assert.Single(Directory.GetFiles(Path.Combine(_dir, "local", "crashes"))));
        Assert.Contains("Winshell closed: yes", report, StringComparison.Ordinal);
        Assert.Contains("Where: Winshell", report, StringComparison.Ordinal);
        Assert.Contains("Shell name: probe", report, StringComparison.Ordinal);
        Assert.Contains("The probe was asked to crash as it started.", report, StringComparison.Ordinal);
        Assert.Contains("Shell name: \"probe\"", result.Error + result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("ObjectDisposedException", result.Error + result.Output, StringComparison.Ordinal);
        var logged = File.ReadAllText(LogLine().Match(report).Groups["path"].Value);
        Assert.Contains("FATAL: Winshell crashed: System.InvalidOperationException", logged, StringComparison.Ordinal);
        Assert.Contains("Wrote a crash report to", logged, StringComparison.Ordinal);
        Assert.Empty(Listed());
        Directory.Delete(Path.Combine(_dir, "local", "crashes"), true);

        var next = ProbeShell.Launch(Path.Combine(_dir, "config"), Path.Combine(_dir, "local"), ["--no-duplicate"]);
        _started.Add(next);
        Assert.Equal(next.Id, NextShell(0));
        Assert.False(next.HasExited);
    }

    [Fact]
    [Trait("Category", "Window")]
    public void A_crashed_shell_starts_again_with_the_taskbar_back()
    {
        _taskbarHidden = ExplorerTaskbarWindow.AnyHidden();
        Assert.False(_taskbarHidden, "Explorer's taskbar was hidden before the test.");
        var marker = Path.Combine(_dir, "config", ProbeShell.HideTaskbarMarker);
        File.WriteAllText(marker, "");
        var run = Supervise();
        var first = NextShell(0);
        Wait.Until(() => !File.Exists(marker) && ExplorerTaskbarWindow.AnyHidden(), "the shell to hide the taskbar");

        using (var crashed = Process.GetProcessById(first))
        {
            crashed.Kill();
            crashed.WaitForExit();
        }

        NextShell(first);

        Assert.False(ExplorerTaskbarWindow.AnyHidden());
        Assert.False(run.HasExited);
    }

    [Fact]
    public void A_forced_stop_of_a_hung_shell_ends_supervision()
    {
        File.WriteAllText(Path.Combine(_dir, "config", ProbeShell.HangMarker), "");
        var run = Supervise();
        var shell = NextShell(0);
        var pidFile = Path.Combine(_dir, "config", "child.pid");
        Wait.Until(() => File.Exists(pidFile), "the shell's program to start");
        using var program = Process.GetProcessById(int.Parse(File.ReadAllText(pidFile), CultureInfo.InvariantCulture));
        try
        {
            var stopped = Cli("stop", "--force", "--pid", shell.ToString(CultureInfo.InvariantCulture));

            stopped.AssertExitCode(0);
            Wait.Until(() => run.HasExited, "winshell run to end with its shell");
            ChildProcess.Exited("winshell run", run, _printed).AssertExitCode(-1);
            Assert.Empty(Listed());
        }
        finally
        {
            program.Kill();
            program.WaitForExit(5000);
        }
    }

    private Process Supervise()
    {
        var info = WinshellCli.StartInfo(["run", "--no-build", "-p", Project]);
        foreach (var (name, value) in Folders)
            info.Environment[name] = value;
        var run = ChildProcess.Start(info, _printed);
        _started.Add(run);
        return run;
    }

    private int NextShell(int previous)
    {
        var pid = 0;
        Wait.Until(() => (pid = new Instances(Path.Combine(_dir, "local", "run")).Running()
            .Select(info => info.Pid).FirstOrDefault(p => p != previous)) != 0, "the shell to publish its instance");
        return pid;
    }

    private List<int> Listed()
    {
        return Rows().Select(row => row.GetProperty("pid").GetInt32()).ToList();
    }

    private List<JsonElement> Rows()
    {
        using var listed = JsonDocument.Parse(Cli("list", "--json").Output);
        return listed.RootElement.EnumerateArray().Select(row => row.Clone()).ToList();
    }

    private ChildProcess Cli(params string[] args)
    {
#pragma warning disable CsWinRT1030
        return WinshellCli.Run(args, Folders);
#pragma warning restore CsWinRT1030
    }

    public void Dispose()
    {
        foreach (var run in _started)
        {
            if (!run.HasExited)
            {
                run.Kill(true);
                run.WaitForExit();
            }

            run.Dispose();
        }

        if (_taskbarHidden == false)
        {
            ExplorerTaskbarWindow.RestoreHidden();
            AppBars.SetState(_appBarState);
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
