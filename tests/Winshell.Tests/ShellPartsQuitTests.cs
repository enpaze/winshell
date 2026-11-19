using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Winshell.Tests;

public sealed class ShellPartsQuitTests : IDisposable, IClassFixture<ProbeBuild>
{
    private readonly string _dir = TempFolder.NewPath();
    private readonly string _config;
    private readonly string _local;
    private readonly StringBuilder _printed = new();
    private readonly List<Process> _started = [];

    public ShellPartsQuitTests()
    {
        _config = Path.Combine(_dir, "config");
        _local = Path.Combine(_dir, "local");
        Directory.CreateDirectory(_config);
    }

    [Fact]
    public void Quitting_disposes_each_added_part_once_last_first_and_after_the_authors_own_dispose()
    {
        var probe = Launch("quit");

        Assert.True(probe.WaitForExit(TimeSpan.FromSeconds(60)), "The probe did not quit.");
        ChildProcess.Exited("The probe", probe, _printed).AssertExitCode(0);
        Assert.Equal(Disposed(probe.Id, "quit"), File.ReadAllLines(Path.Combine(_config, "parts-quit.txt")));
        Assert.Empty(CrashReports());
    }

    [Fact]
    public void A_reload_disposes_the_added_parts_in_the_process_that_hands_over()
    {
        var probe = Launch("reload");

        Assert.True(probe.WaitForExit(TimeSpan.FromSeconds(60)), "The probe did not reload.");
        ChildProcess.Exited("The probe", probe, _printed).AssertExitCode(0);
        Assert.Equal([.. Disposed(probe.Id, "reload"), $"{probe.Id} reloaded True"],
            File.ReadAllLines(Path.Combine(_config, "parts-reload.txt")));

        var next = Path.Combine(_config, "parts-quit.txt");
        var started = Array.Empty<string>();
        Wait.Until(() => (started = Lines(next)) is [var line, ..] && line.EndsWith(" added", StringComparison.Ordinal),
            "the next probe to start", TimeSpan.FromSeconds(60));
        var nextId = int.Parse(started[0].Split(' ')[0], CultureInfo.InvariantCulture);
        Assert.NotEqual(probe.Id, nextId);
        WaitForExit(nextId);
        Assert.Equal(Disposed(nextId, "quit"), File.ReadAllLines(next));
        Assert.Empty(CrashReports());
    }

    private Process Launch(string ending)
    {
        File.WriteAllText(Path.Combine(_config, ProbeShell.PartsMarker), ending);
        var probe = ProbeShell.Launch(_config, _local, [], _printed);
        _started.Add(probe);
        return probe;
    }

    private static string[] Disposed(int pid, string ending)
    {
        return
        [
            $"{pid} dispose disposed added",
            $"{pid} {ending}",
            $"{pid} dispose disposed refused",
            $"{pid} dispose second refused",
            $"{pid} dispose first refused"
        ];
    }

    private static string[] Lines(string path)
    {
        if (!File.Exists(path))
            return [];
        using var reader = new StreamReader(SharedFile.OpenToRead(path));
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private string[] CrashReports()
    {
        var crashes = Path.Combine(_local, "crashes");
        return Directory.Exists(crashes) ? Directory.GetFiles(crashes) : [];
    }

    private void WaitForExit(int pid)
    {
        Process next;
        try
        {
            next = Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            return;
        }

        _started.Add(next);
        Assert.True(next.WaitForExit(TimeSpan.FromSeconds(60)), "The next probe did not quit.");
    }

    public void Dispose()
    {
        foreach (var process in _started)
        {
            if (!process.HasExited)
            {
                process.Kill();
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
