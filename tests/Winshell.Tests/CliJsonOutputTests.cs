using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Winshell.Tests;

public sealed partial class CliJsonOutputTests : IDisposable, IClassFixture<ProbeBuild>
{
    private readonly string _dir = TempFolder.NewPath();
    private Process? _started;

    private string Local => Path.Combine(_dir, "café");

    [Fact]
    public void List_json_drops_file_fields_and_ipc_show_json_lists_the_target()
    {
        Directory.CreateDirectory(Local);
        Directory.CreateDirectory(Path.Combine(_dir, "config"));
        var process = _started = ProbeShell.Start(Path.Combine(_dir, "config"), Local);

        var list = Run("list", "--json");
        list.AssertExitCode(0);
        Assert.Contains("café", list.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u00E9", list.Output, StringComparison.OrdinalIgnoreCase);
        using var listed = JsonDocument.Parse(list.Output);
        Assert.Equal(JsonValueKind.Array, listed.RootElement.ValueKind);
        var row = listed.RootElement.EnumerateArray().Single(i => i.GetProperty("pid").GetInt32() == process.Id);
        Assert.False(row.TryGetProperty("processStart", out _));
        Assert.False(row.TryGetProperty("exitedAt", out _));
        Assert.False(string.IsNullOrEmpty(row.GetProperty("id").GetString()));
        Assert.False(string.IsNullOrEmpty(row.GetProperty("shellName").GetString()));
        Assert.Contains("café", row.GetProperty("log").GetString(), StringComparison.Ordinal);
        _ = row.GetProperty("launchTime").GetDateTime();
        Assert.False(string.IsNullOrEmpty(row.GetProperty("pipe").GetString()));
        Assert.False(string.IsNullOrEmpty(row.GetProperty("log").GetString()));

        var show = Run("ipc", "--pid", process.Id.ToString(CultureInfo.InvariantCulture), "show", "-j");
        show.AssertExitCode(0);
        Assert.DoesNotContain("\\u00E9", show.Output, StringComparison.OrdinalIgnoreCase);
        using var shown = JsonDocument.Parse(show.Output);
        Assert.Equal(JsonValueKind.Array, shown.RootElement.ValueKind);
        var target = shown.RootElement.EnumerateArray().Single(t => t.GetProperty("target").GetString() == "probe");
        var note = Assert.Single(target.GetProperty("functions").EnumerateArray());
        Assert.Equal("note", note.GetProperty("name").GetString());
        Assert.Equal("void", note.GetProperty("returns").GetString());
        var parameter = Assert.Single(note.GetProperty("parameters").EnumerateArray());
        Assert.Equal("text", parameter.GetProperty("name").GetString());
        Assert.Equal("string", parameter.GetProperty("type").GetString());
    }

    [Fact]
    public void List_prints_the_shell_name_and_program_of_each_running_instance()
    {
        Directory.CreateDirectory(Local);
        Directory.CreateDirectory(Path.Combine(_dir, "config"));
        var process = _started = ProbeShell.Start(Path.Combine(_dir, "config"), Local);

        var list = Run("list");

        list.AssertExitCode(0);
        var lines = list.Output.ReplaceLineEndings("\n").Split('\n');
        var pid = Array.IndexOf(lines, $"  Process ID: {process.Id}");
        Assert.True(pid > 0, list.Output);
        Assert.StartsWith("Instance ", lines[pid - 1], StringComparison.Ordinal);
        Assert.Equal("  Shell name: probe", lines[pid + 1]);
        Assert.Equal($"  Program: {Path.GetFullPath(ProbeShell.Program)}", lines[pid + 2], ignoreCase: true);
    }

    private ChildProcess Run(params string[] args)
    {
#pragma warning disable CsWinRT1030
        return WinshellCli.Run(args, IsolatedFolders.Of(Path.Combine(_dir, "config"), Local));
#pragma warning restore CsWinRT1030
    }

    public void Dispose()
    {
        if (_started is { } process)
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
