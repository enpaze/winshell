using Winshell.Apps;

namespace Winshell.Services.Tests;

/// <summary>One command line parses and writes the words sign-in, a shell's reload and an app launch share.</summary>
public sealed class CommandLineTests
{
    [Fact]
    public void A_word_with_no_short_form_says_so()
    {
        Assert.Null(CommandWords.NoBuild.Short);
        Assert.Null(CommandWords.After.Short);
        Assert.Equal("-p", CommandWords.Path.Short);
        Assert.Equal("-c", CommandWords.Config.Short);
        Assert.Equal("-d", CommandWords.Daemonize.Short);
        Assert.Equal("-n", CommandWords.NoDuplicate.Short);
        Assert.Equal("-v", CommandWords.Verbose.Short);
    }

    [Theory]
    [InlineData(@"C:\My Shell\")]
    [InlineData("say \"hi\"")]
    [InlineData("")]
    public void A_quoted_argument_parses_back_to_itself(string argument)
    {
        // The program name is argv[0], which Windows parses without backslash escapes. The quoted text is an argument.
        var written = CommandLine.Join(argument, "");
        Assert.Equal(argument, CommandLine.Parse("winshell.exe " + written).Arguments[1]);
    }

    [Fact]
    public void An_app_launch_quotes_its_program_the_way_Windows_reads_it_back()
    {
        var app = new AppRecord(
            "id", "Demo", "", @"C:\notes\Demo.lnk", @"C:\My Shell\Demo.exe", "\"/user name\"", "", "", "", false, false);
        var gone = new AppRecord("id", "Gone", "", @"C:\My Notes\", "", "", "", "", "", false, false);

        Assert.Equal([@"C:\My Shell\Demo.exe", "/user name"], CommandLine.Parse(app.CommandLine).Arguments);
        Assert.Equal(["explorer.exe", @"C:\My Notes\"], CommandLine.Parse(gone.CommandLine).Arguments);
    }
}
