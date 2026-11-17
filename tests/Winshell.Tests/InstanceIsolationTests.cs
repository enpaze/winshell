using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Xunit.Sdk;

namespace Winshell.Tests;

public sealed partial class InstanceIsolationTests : IDisposable, IClassFixture<ProbeBuild>
{
    private readonly string _dir = TempFolder.NewPath();
    private readonly List<(Process Process, string Local)> _started = [];

    private string Local => Path.Combine(_dir, "local");

    private string RunDir(string id)
    {
        return Path.Combine(Local, "run", id);
    }

    private string Config(string folder)
    {
        var dir = Path.Combine(_dir, folder);
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Instances_keep_apart_and_outlive_one_that_crashes()
    {
        var locals = new[] { Path.Combine(_dir, "local-a"), Path.Combine(_dir, "local-b"), Path.Combine(_dir, "local-c") };
        var a = Start(Config("a"), locals[0]);
        var b = Start(Config("b"), locals[1]);
        var c = Start(Config("c"), locals[2], "other");
        var infos = new[] { Info(a, locals[0]), Info(b, locals[1]), Info(c, locals[2]) };

        Assert.Equal(3, infos.Select(i => i.GetProperty("id").GetString()).Distinct().Count());
        Assert.Equal(3, infos.Select(i => i.GetProperty("pipe").GetString()).Distinct().Count());
        Assert.Equal(3, infos.Select(i => i.GetProperty("log").GetString()).Distinct().Count());
        Assert.Equal("probe", infos[0].GetProperty("shellId").GetString());
        Assert.Equal("probe", infos[1].GetProperty("shellId").GetString());
        Assert.Equal("other", infos[2].GetProperty("shellId").GetString());
        Assert.Equal(0, Show(a, locals[0]));
        Assert.Equal(0, Show(b, locals[1]));
        Assert.Equal(0, Show(c, locals[2]));

        b.Kill();
        b.WaitForExit();

        Assert.Equal(0, Show(a, locals[0]));
        Assert.Equal(0, Show(c, locals[2]));
        Assert.DoesNotContain(Listed(locals[1]), i => i.GetProperty("pid").GetInt32() == b.Id);
    }

    [Fact]
    public void A_reused_process_id_is_listed_for_the_instance_that_owns_it()
    {
        using var process = Process.GetCurrentProcess();
        var liveId = Instances.NewId();
        var instances = new Instances(Path.Combine(Local, "run"));
        var live = new InstanceInfo(liveId, process.Id, "live", Environment.ProcessPath!,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            "winshell-" + liveId, Path.Combine(RunDir(liveId), "log.txt"),
            process.StartTime.ToUniversalTime().Ticks, ProcessName: process.ProcessName);
        instances.Write(live);
        var reusedId = Instances.NewId();
        var reusedDir = RunDir(reusedId);
        Directory.CreateDirectory(reusedDir);
        instances.Write(live with
        {
            Id = reusedId,
            ShellId = "reused",
            LaunchTime = live.LaunchTime.AddMinutes(1),
            Pipe = "winshell-" + reusedId,
            Log = Path.Combine(reusedDir, "log.txt"),
            ProcessStart = 1
        });

        var rows = Listed(Local).Where(i => i.GetProperty("pid").GetInt32() == process.Id).ToList();
        Assert.Single(rows);
        Assert.Equal(liveId, rows[0].GetProperty("id").GetString());
    }

    private Process Start(string configDir, string local, string? shellId = null)
    {
        var process = ProbeShell.Start(configDir, local, shellId);
        _started.Add((process, local));
        return process;
    }

    private JsonElement Info(Process process, string local)
    {
        return Listed(local).Single(i => i.GetProperty("pid").GetInt32() == process.Id);
    }

    private List<JsonElement> Listed(string local)
    {
        using var json = JsonDocument.Parse(Winshell(local, "list", "--json"));
        return json.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private int Show(Process process, string local)
    {
        var result = WinshellCli.Run(["ipc", "--pid", process.Id.ToString(CultureInfo.InvariantCulture), "show"],
            ("WINSHELL_LOCAL_DIR", local));
        result.AssertExitCode(0);
        return result.Code;
    }

    private string Winshell(string local, params string[] args)
    {
#pragma warning disable CsWinRT1030
        var result = WinshellCli.Run(args, ("WINSHELL_LOCAL_DIR", local));
#pragma warning restore CsWinRT1030
        result.AssertExitCode(0);
        return result.Output;
    }

    public void Dispose()
    {
        foreach (var (process, local) in _started)
        {
            if (!process.HasExited)
            {
                try
                {
                    Winshell(local, "kill", "--pid", process.Id.ToString(CultureInfo.InvariantCulture));
                }
                catch (XunitException)
                {
                }

                if (!process.WaitForExit(5000))
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
