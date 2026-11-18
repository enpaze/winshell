namespace Winshell.Tests;

[TestClass]
public class CliHelpTextTests
{
    [TestMethod]
    [DataRow("summary", "")]
    [DataRow("run", "run")]
    [DataRow("list", "list")]
    [DataRow("kill", "kill")]
    [DataRow("reload", "reload")]
    [DataRow("dev", "dev")]
    [DataRow("log", "log")]
    [DataRow("ipc", "ipc")]
    [DataRow("inspect", "inspect")]
    [DataRow("doctor", "doctor")]
    [DataRow("new", "new")]
    [DataRow("use", "use")]
    [DataRow("update", "update")]
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

        Assert.AreEqual(golden, actual);
    }
}
