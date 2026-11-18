namespace Winshell.Tests;

public class CliHelpTextTests
{
    [Theory]
    [InlineData("summary", "")]
    [InlineData("run", "run")]
    [InlineData("list", "list")]
    [InlineData("kill", "kill")]
    [InlineData("reload", "reload")]
    [InlineData("dev", "dev")]
    [InlineData("log", "log")]
    [InlineData("ipc", "ipc")]
    [InlineData("inspect", "inspect")]
    [InlineData("doctor", "doctor")]
    [InlineData("new", "new")]
    [InlineData("use", "use")]
    [InlineData("update", "update")]
    public void Help_matches_the_recorded_text(string file, string command)
    {
        var golden = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "HelpText", file + ".txt"))
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd();
        var help = CliParser.Help(command.Length == 0 ? null : command);
        var actual = help
            .Replace(ShellLocations.Current.ConfigRoot, "{config-root}", StringComparison.Ordinal)
            .Replace(ShellLocations.ConfigVariable, "{config-override}", StringComparison.Ordinal)
            .Replace(Shell.Version, "{version}", StringComparison.Ordinal)
            .TrimEnd();

        Assert.Equal(golden, actual);
    }
}
