using System.Diagnostics;
using System.Globalization;
using Winshell.Commands;

namespace Winshell.Tests;

/// <summary>
/// <c>winshell use</c> builds a configuration that needs it, records it only when it built, and switches the running
/// shell to it. The switch starts the probe, which holds no taskbar, edge or window.
/// </summary>
public sealed class UseCommandTests : IDisposable, IClassFixture<ProbeBuild>
{
    private static readonly DateTime Long = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly string _dir = TempFolder.NewPath();
    private readonly List<Process> _started = [];

    private string Config => Path.Combine(_dir, "config");

    private string Local => Path.Combine(_dir, "local");

    public UseCommandTests()
    {
        Directory.CreateDirectory(Config);
        Directory.CreateDirectory(Local);
    }

    [Fact]
    public void A_config_with_no_build_is_built_and_a_failed_build_keeps_the_choice()
    {
        Built(Project("a"));
        Project("b");

        Assert.NotNull(UseCommand.Choose("a", Config, Fails, out _));
        Assert.Null(UseCommand.Choose("b", Config, Fails, out var error));

        Assert.Equal("a", ConfigurationProject.Chosen(Config));
        Assert.Contains("error CS1002", error, StringComparison.Ordinal);
        Assert.EndsWith("\nNothing changed.", error, StringComparison.Ordinal);

        Assert.NotNull(UseCommand.Choose("b", Config, Builds, out _));

        Assert.Equal("b", ConfigurationProject.Chosen(Config));
        Assert.True(File.Exists(Path.Combine(Config, "b", "bin", "shell", "b.exe")));
        return;

        static CommandResult Builds(string file, IReadOnlyList<string> arguments, string folder)
        {
            var output = arguments[arguments.ToList().IndexOf("-o") + 1];
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "b.exe"), "");
            return new CommandResult(0, "");
        }
    }

    [Fact]
    public void Use_stops_the_running_shell_before_it_starts_the_chosen_one()
    {
        var other = ProbeShell.Start(Config, Local);
        _started.Add(other);
        var otherId = Running().Single().Id;
        // b is the probe's program: its bin folder is the probe's, and its project is older than that program.
        var b = Project("b", "<Project><PropertyGroup><AssemblyName>Probe</AssemblyName></PropertyGroup></Project>");
        File.SetLastWriteTimeUtc(b, Long);
        var probeBin = Path.GetDirectoryName(Path.GetDirectoryName(ProbeShell.Program))!;
        ChildProcess.Run("cmd.exe", "/c", "mklink", "/J", Path.Combine(Config, "b", "bin"), probeBin)
            .AssertExitCode(0);

        var result = Cli("use", "b");

        result.AssertExitCode(0);
        result.AssertNoError();
        var newLine = Environment.NewLine;
        Assert.Equal($"Chose b.{newLine}Stopped {otherId}{newLine}Started b{newLine}", result.Output);
        Assert.True(other.HasExited);
        Wait.Until(() => Running().Count == 1, "the chosen shell");
        var started = Running().Single();
        Assert.Equal(Path.Combine(Config, "b", "bin", "shell", "Probe.exe"), started.ConfigPath, true);
        using var process = Process.GetProcessById(started.Pid);
        Assert.True(other.ExitTime <= process.StartTime);
        Assert.Equal($"* b{newLine}", Cli("use").Output);
    }

    private static CommandResult Fails(string file, IReadOnlyList<string> arguments, string folder)
    {
        return new CommandResult(1, Path.Combine(folder, "Program.cs") + "(1,1): error CS1002: ; expected");
    }

    private string Project(string name, string text = "<Project Sdk=\"Microsoft.NET.Sdk\" />")
    {
        var file = Path.Combine(Config, name, name + ".csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
        return file;
    }

    private static void Built(string projectFile)
    {
        var program = Path.Combine(Path.GetDirectoryName(projectFile)!, "bin", "shell",
            Path.GetFileNameWithoutExtension(projectFile) + ".exe");
        Directory.CreateDirectory(Path.GetDirectoryName(program)!);
        File.WriteAllText(program, "");
        File.SetLastWriteTimeUtc(projectFile, Long);
    }

    private List<InstanceInfo> Running()
    {
        return new Instances(Path.Combine(Local, "run")).List(false).Select(row => row.Info).ToList();
    }

    private ChildProcess Cli(params string[] args)
    {
#pragma warning disable CsWinRT1030 // Not marshalled across the WinRT ABI.
        return WinshellCli.Run(args, ("WINSHELL_CONFIG_DIR", Config), ("WINSHELL_LOCAL_DIR", Local));
#pragma warning restore CsWinRT1030
    }

    public void Dispose()
    {
        // Asked, not ended: the winshell run that supervises the chosen shell would start an ended one again.
        foreach (var info in Running())
            Cli("kill", "--force", "--pid", info.Pid.ToString(CultureInfo.InvariantCulture));
        foreach (var process in _started)
        {
            if (!process.HasExited)
                process.Kill(true);
            process.Dispose();
        }

        // The junction goes first, so nothing deletes the probe's own build through it.
        var junction = Path.Combine(Config, "b", "bin");
        if (Directory.Exists(junction) && File.GetAttributes(junction).HasFlag(FileAttributes.ReparsePoint))
            Directory.Delete(junction);
        Wait.Until(Removed, "the test folder to be free");
    }

    private bool Removed()
    {
        try
        {
            Directory.Delete(_dir, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
