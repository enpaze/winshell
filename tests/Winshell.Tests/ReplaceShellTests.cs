using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Winshell.Tests;

[Collection(ExplorerTaskbarCollection.Name)]
public sealed class ReplaceShellTests : IDisposable, IClassFixture<ProbeBuild>
{
    private readonly string _dir = TempFolder.NewPath();
    private readonly List<Process> _started = [];
    private bool _restoreTaskbar;

    [Fact]
    public void Starting_a_shell_replaces_the_one_that_is_running()
    {
        var local = Folder("local");
        var first = Start(Folder("a"), local);
        var printed = new StringBuilder();
        var second = Launch(Folder("b"), local, "other", printed);

        Wait.Until(() => second.HasExited || Running(local).Any(info => info.Pid == second.Id),
            "the replacement to publish or to fail");

        Assert.False(second.HasExited, printed.ToString());
        Assert.True(first.HasExited);
        first.WaitForExit();
        Assert.Equal(0, first.ExitCode);
        var only = Assert.Single(Running(local));
        Assert.Equal(("other", second.Id), (only.ShellId, only.Pid));
    }

    [Fact]
    public void A_shell_that_does_not_quit_stays_and_the_start_says_why()
    {
        var config = Folder("hang");
        File.WriteAllText(Path.Combine(config, ProbeShell.HangMarker), "");
        var local = Folder("local-hang");
        var first = Start(config, local);
        Wait.Until(() => File.Exists(Path.Combine(config, "child.pid")), "the shell's program to start");
        var printed = new StringBuilder();
        var second = Launch(Folder("next"), local, "other", printed);

        Wait.Until(() => second.HasExited, "the second start to give up");
        second.WaitForExit();

        Assert.Equal(1, second.ExitCode);
        Assert.Contains("Shell probe did not quit", printed.ToString(), StringComparison.Ordinal);
        Assert.False(first.HasExited);
    }

    [Fact]
    [Trait("Category", "Window")]
    public void Replacing_a_shell_keeps_the_taskbar_hidden_until_the_new_one_exits()
    {
        var hidden = ExplorerTaskbarRecovery.AnyHidden();
        _restoreTaskbar = !hidden;
        Assert.False(hidden, "Explorer's taskbar was hidden before the test.");
        var local = Folder("local-taskbar");
        var firstConfig = Folder("taskbar-a");
        var secondConfig = Folder("taskbar-b");
        var markerA = Path.Combine(firstConfig, ProbeShell.HideTaskbarMarker);
        var markerB = Path.Combine(secondConfig, ProbeShell.HideTaskbarMarker);
        File.WriteAllText(markerA, "");
        File.WriteAllText(markerB, "");
        var first = Start(firstConfig, local);
        Wait.Until(() => !File.Exists(markerA) && ExplorerTaskbarRecovery.AnyHidden(),
            "the first shell to hide the taskbar");

        var printed = new StringBuilder();
        var second = Launch(secondConfig, local, "other", printed);
        var flashed = false;
        var started = TimeProvider.System.GetTimestamp();
        var ready = false;
        while (TimeProvider.System.GetElapsedTime(started) < TimeSpan.FromSeconds(30))
        {
            if (!ExplorerTaskbarRecovery.AnyHidden())
                flashed = true;
            ready = !File.Exists(markerB) && Running(local).Any(info => info.Pid == second.Id);
            if (second.HasExited || (ready && first.HasExited))
                break;
            Thread.Sleep(20);
        }

        Assert.False(second.HasExited, printed.ToString());
        Assert.True(ready);
        Assert.True(first.HasExited);
        Assert.False(flashed, "Explorer's taskbar was shown while the shell was replaced.");
        Assert.True(ExplorerTaskbarRecovery.AnyHidden());

#pragma warning disable CsWinRT1030
        var killed = WinshellCli.Run(["kill", "--pid", second.Id.ToString(CultureInfo.InvariantCulture)],
            ("WINSHELL_LOCAL_DIR", local), ("WINSHELL_CONFIG_DIR", secondConfig));
#pragma warning restore CsWinRT1030
        killed.AssertExitCode(0);
        Wait.Until(() => second.HasExited, "the replacement to exit");
        Assert.False(ExplorerTaskbarRecovery.AnyHidden());
    }

    private string Folder(string name)
    {
        var path = Path.Combine(_dir, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private Process Start(string config, string local)
    {
        var process = ProbeShell.Start(config, local);
        _started.Add(process);
        return process;
    }

    private Process Launch(string config, string local, string shellId, StringBuilder printed)
    {
        var process = ProbeShell.Launch(config, local, [], shellId, printed);
        _started.Add(process);
        return process;
    }

    private static List<InstanceInfo> Running(string local)
    {
        return new Instances(Path.Combine(local, "run")).List(false).Select(row => row.Info).ToList();
    }

    public void Dispose()
    {
        foreach (var pidFile in Directory.Exists(_dir)
                     ? Directory.EnumerateFiles(_dir, "child.pid", SearchOption.AllDirectories)
                     : [])
        {
            if (!int.TryParse(File.ReadAllText(pidFile), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                continue;
            try
            {
                using var child = Process.GetProcessById(pid);
                if (!child.HasExited)
                    child.Kill();
            }
            catch (Exception)
            {
            }
        }

        foreach (var process in _started)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(true);
            }
            catch (Exception)
            {
            }

            process.Dispose();
        }

        if (_restoreTaskbar)
            ExplorerTaskbarRecovery.RestoreHidden();
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
