namespace Winshell.Tests;

public sealed partial class ConfigPathTests : IDisposable
{
    private readonly string _root =
        TempFolder.NewPath();

    public ConfigPathTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void The_cli_uses_winshell_config_dir()
    {
        var config = Path.Combine(_root, "configs");
        Directory.CreateDirectory(config);

        var help = Run(config, "--help");
        help.AssertExitCode(0);
        Assert.Contains(config, help.Output, StringComparison.OrdinalIgnoreCase);

        var missing = Run(config, "-s", "missing");
        missing.AssertExitCode(1);
        Assert.Contains(config, missing.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing", missing.Error, StringComparison.Ordinal);
    }

    private static ChildProcess Run(string configDir, params string[] args)
    {
#pragma warning disable CsWinRT1030
        return WinshellCli.Run(args, (ShellLocations.ConfigVariable, configDir));
#pragma warning restore CsWinRT1030
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }
}
