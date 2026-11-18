using Winshell.Apps;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class CommandLineTests
{
    [TestMethod]
    public void A_word_with_no_short_form_says_so()
    {
        Assert.IsNull(CommandWords.NoBuild.Short);
        Assert.IsNull(CommandWords.After.Short);
        Assert.AreEqual("-p", CommandWords.Path.Short);
        Assert.AreEqual("-c", CommandWords.Config.Short);
        Assert.AreEqual("-d", CommandWords.Daemonize.Short);
        Assert.AreEqual("-n", CommandWords.NoDuplicate.Short);
        Assert.AreEqual("-v", CommandWords.Verbose.Short);
    }

    [TestMethod]
    [DataRow(@"C:\My Shell\")]
    [DataRow("say \"hi\"")]
    [DataRow("")]
    public void A_quoted_argument_parses_back_to_itself(string argument)
    {
        var written = CommandLine.Join(argument, "");
        Assert.AreEqual(argument, CommandLine.Parse("winshell.exe " + written).Arguments[1]);
    }

    [TestMethod]
    public void An_app_launch_quotes_its_program_the_way_Windows_reads_it_back()
    {
        var app = new AppRecord(
            "id", "Demo", "", @"C:\notes\Demo.lnk", @"C:\My Shell\Demo.exe", "\"/user name\"", "", "", "", false, false);
        var gone = new AppRecord("id", "Gone", "", @"C:\My Notes\", "", "", "", "", "", false, false);

        Assert.AreSequenceEqual([@"C:\My Shell\Demo.exe", "/user name"], CommandLine.Parse(app.CommandLine).Arguments);
        Assert.AreSequenceEqual(["explorer.exe", @"C:\My Notes\"], CommandLine.Parse(gone.CommandLine).Arguments);
    }
}
