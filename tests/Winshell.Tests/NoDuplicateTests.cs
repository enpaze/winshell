using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Winshell.Tests;

[TestClass]
public sealed partial class NoDuplicateTests : ProbeBuild, IDisposable
{
    private readonly string _dir = TempFolder.NewPath();
    private readonly string _local;
    private readonly string _config;
    private readonly List<Process> _started = [];
    private readonly Dictionary<int, StringBuilder> _printed = [];

    public NoDuplicateTests()
    {
        _local = Path.Combine(_dir, "local");
        _config = Path.Combine(_dir, "config");
        Directory.CreateDirectory(_local);
        Directory.CreateDirectory(_config);
    }

    [TestMethod]
    public async Task A_second_start_exits_and_another_shell_stays()
    {
        var first = Start(true);
        AssertHeld("probe");

        var same = Launch(true);
        await AssertExitedQuietly(same);
        Assert.IsFalse(first.HasExited);

        var second = Start(true, ProbeShell.Other);
        Assert.IsTrue(first.HasExited);
        Assert.AreEqual(0, first.ExitCode);
        Assert.IsFalse(second.HasExited);
        Assert.AreEqual("other", Assert.ContainsSingle(Running()).ShellName);
        AssertHeld("other");
    }

    [TestMethod]
    public async Task A_shell_started_without_the_flag_still_blocks_a_duplicate()
    {
        var first = Start(false);
        Assert.ThrowsExactly<WaitHandleCannotBeOpenedException>(() => Mutex.OpenExisting(MutexName("probe")));

        await AssertExitedQuietly(Launch(true));
        Assert.IsFalse(first.HasExited);
    }

    [TestMethod]
    public async Task A_copy_of_the_shell_at_another_path_is_still_a_duplicate()
    {
        var first = Start(false);
        Assert.AreNotEqual(Path.GetFullPath(ProbeShell.Program), Path.GetFullPath(ProbeShell.Copy));

        await AssertExitedQuietly(Launch(true, ProbeShell.Copy));
        Assert.IsFalse(first.HasExited);
        Assert.AreEqual(first.Id, Assert.ContainsSingle(Running()).Pid);
    }

    [TestMethod]
    public async Task Two_overlapping_starts_leave_one_shell()
    {
        var first = Launch(true);
        var second = Launch(true);

        Wait.Until(() => first.HasExited || second.HasExited, "one of the two starts to exit");
        var loser = first.HasExited ? first : second;
        var winner = ReferenceEquals(loser, first) ? second : first;
        await AssertExitedQuietly(loser);

        Wait.Until(() => Running().Any(i => i.Pid == winner.Id), $"instance {winner.Id}");
        Assert.IsFalse(winner.HasExited);
        var listed = Listed();
        Assert.Contains(i => i.GetProperty("pid").GetInt32() == winner.Id, listed);
        Assert.DoesNotContain(i => i.GetProperty("pid").GetInt32() == loser.Id, listed);
    }

    [TestMethod]
    public async Task Daemonize_leaves_the_mutex_to_the_child()
    {
        ProbeShell.Detach(_config, _local, ["--no-duplicate", "-d"]).AssertExitCode(0);

        Wait.Until(() => Running().Any(i => i.ShellName == "probe"), "the background shell");
        var pid = Listed().Single(i => i.GetProperty("shellName").GetString() == "probe").GetProperty("pid").GetInt32();
        var child = Process.GetProcessById(pid);
        _started.Add(child);
        AssertHeld("probe");

        await AssertExitedQuietly(Launch(true));
        Assert.IsFalse(child.HasExited);
    }

    private Process Start(bool duplicate, string? program = null)
    {
        var process = Launch(duplicate, program);
        Wait.Until(() => Running().Any(i => i.Pid == process.Id),
            $"instance {process.Id} in winshell list");
        return process;
    }

    private Process Launch(bool duplicate, string? program = null)
    {
        var printed = new StringBuilder();
        var process = ProbeShell.Launch(_config, _local, duplicate ? ["--no-duplicate"] : [], printed, program);
        _started.Add(process);
        _printed[process.Id] = printed;
        return process;
    }

    private async Task AssertExitedQuietly(Process process)
    {
        Wait.Until(() => process.HasExited, "the duplicate to exit");
        await OwnThread.Run(process.WaitForExit).WaitAsync(CurrentTest.Cancellation);
        ChildProcess.Exited("The probe", process, _printed[process.Id]).AssertExitCode(0);
        Assert.AreEqual("", _printed[process.Id].ToString());
    }

    private void AssertHeld(string shellName)
    {
        using var mutex = Mutex.OpenExisting(MutexName(shellName));
        var taken = false;
        try
        {
            taken = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            taken = true;
        }
        finally
        {
            if (taken)
                mutex.ReleaseMutex();
        }

        Assert.IsFalse(taken);
    }

    private string MutexName(string shellName)
    {
        return HeldMutex.NoDuplicateName(new ShellLocations(_config, _local), shellName);
    }

    private List<JsonElement> Listed()
    {
        var result = Run("list", "--json");
        result.AssertExitCode(0);
        using var json = JsonDocument.Parse(result.Output);
        return json.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private List<InstanceInfo> Running()
    {
        return new Instances(Path.Combine(_local, "run")).Running();
    }

    private ChildProcess Run(params string[] args)
    {
#pragma warning disable CsWinRT1030
        return WinshellCli.Run(args, IsolatedFolders.Of(_config, _local));
#pragma warning restore CsWinRT1030
    }

    public void Dispose()
    {
        foreach (var process in _started.ToList())
        {
            try
            {
                if (!process.HasExited)
                {
                    Run("stop", "--pid", process.Id.ToString(CultureInfo.InvariantCulture));
                    if (!process.WaitForExit(8_000))
                        process.Kill(true);
                }
            }
            catch (Exception)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(true);
                }
                catch (Exception)
                {
                }
            }

            process.Dispose();
        }

        try
        {
            Directory.Delete(_dir, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
