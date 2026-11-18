using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Winshell.Tests;

public sealed class ShellPathsTests : IDisposable, IClassFixture<ProbeBuild>, IClassFixture<OtherProbeBuild>
{
    private readonly string _dir = TempFolder.NewPath();
    private readonly List<Process> _started = [];

    private string Config => Path.Combine(_dir, "config");

    private string Local => Path.Combine(_dir, "local");

    [Fact]
    public void Without_folder_properties_the_folders_are_under_the_local_folder_by_shell_name()
    {
        var process = Start(ProbeShell.Program);

        Assert.Equal(Path.Combine(Local, "data", "by-shell", "probe"), Get(process, "data"));
        Assert.Equal(Path.Combine(Local, "state", "by-shell", "probe"), Get(process, "state"));
        Assert.Equal(Path.Combine(Local, "cache", "by-shell", "probe"), Get(process, "cache"));
    }

    [Fact]
    public void A_folder_property_is_in_the_project_folder_or_expands_a_variable_or_the_base()
    {
        var process = Start(ProbeShell.Other);

        var data = Get(process, "data");
        var state = Get(process, "state");
        var cache = Get(process, "cache");

        Assert.Equal(Path.Combine(ProbeShell.OtherFolder, ProbeShell.OtherDataDir), data);
        Assert.Equal(Path.Combine(Local, "state", ProbeShell.OtherStateDir), state);
        Assert.Equal(Path.Combine(Local, ProbeShell.OtherCacheDir), cache);
        Assert.All([data, state, cache], folder => Assert.True(Directory.Exists(folder), folder));
    }

    [Fact]
    public void Winshell_finds_the_log_and_the_shells_folder_where_the_paths_say()
    {
        var process = Start(ProbeShell.Other);
        var pid = process.Id.ToString(CultureInfo.InvariantCulture);
        var logs = Get(process, "logs");

        Assert.Equal(Path.GetDirectoryName(ProbeShell.Other), Get(process, "program"));
        Assert.Equal(Config, Get(process, "shells"));
        using (var listed = JsonDocument.Parse(Run("list", "--json").Output))
        {
            var row = listed.RootElement.EnumerateArray().Single(i => i.GetProperty("pid").GetInt32() == process.Id);
            Assert.Equal(logs, row.GetProperty("log").GetString());
        }

        var log = Run("log", "--pid", pid);
        log.AssertExitCode(0);
        Assert.Contains($"Saving logs to \"{logs}\"", log.Output, StringComparison.Ordinal);

        using var doctor = JsonDocument.Parse(Run("doctor", "--json", "-p", ProbeShell.OtherFolder).Output);
        var instances = doctor.RootElement.GetProperty("checks").EnumerateArray()
            .Single(c => c.GetProperty("id").GetString() == "instances");
        var shown = logs.Replace(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "%LOCALAPPDATA%", StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"log {shown}", instances.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    private Process Start(string program)
    {
        Directory.CreateDirectory(Config);
        var process = ProbeShell.Start(Config, Local, program);
        _started.Add(process);
        return process;
    }

    private string Get(Process process, string path)
    {
        var get = Run("ipc", "--pid", process.Id.ToString(CultureInfo.InvariantCulture), "get", "paths", path);
        get.AssertExitCode(0);
        return get.Output.Trim();
    }

    private ChildProcess Run(params string[] args)
    {
        return WinshellCli.Run(args, IsolatedFolders.Of(Config, Local));
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
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
