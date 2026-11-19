using System.Globalization;

namespace Winshell.Tests;

[TestClass]
public class CommandOptionTests
{
    [TestMethod]
    [DataRow("list --force", "winshell list does not take \"--force\".")]
    [DataRow("list -s work", "winshell list does not take \"-s\".")]
    [DataRow("stop -j", "winshell stop does not take \"-j\".")]
    [DataRow("-d stop", "winshell stop does not take \"-d\".")]
    [DataRow("reload --no-build", "winshell reload does not take \"--no-build\".")]
    [DataRow("inspect --force tree", "winshell inspect does not take \"--force\".")]
    [DataRow("inspect screenshot out.png --fps 30", "winshell inspect screenshot does not take \"--fps\".")]
    [DataRow("inspect tree --screen", "winshell inspect tree does not take \"--screen\".")]
    [DataRow("inspect point Menu --right", "winshell inspect point does not take \"--right\".")]
    [DataRow("dev -j", "winshell dev does not take \"-j\".")]
    [DataRow("doctor --pid 5", "winshell doctor does not take \"--pid\".")]
    [DataRow("run -j", "winshell run does not take \"-j\".")]
    [DataRow("help -j", "winshell help does not take \"-j\".")]
    public void An_option_the_command_does_not_take_is_named(string line, string message)
    {
        var e = Assert.ThrowsExactly<ArgumentException>(() => CliParser.Parse(line.Split(' ')));

        Assert.AreEqual(message, e.Message);
    }

    [TestMethod]
    [DataRow("run -p missing --pth", "Unknown option \"--pth\".")]
    [DataRow("run -p missing --log-times", "Unknown option \"--log-times\".")]
    [DataRow("run -p missing --any-display", "Unknown option \"--any-display\".")]
    [DataRow("run -p missing --newest", "Unknown option \"--newest\".")]
    [DataRow("run -p missing --log-rules x", "Unknown option \"--log-rules\".")]
    [DataRow("run -p missing -m MyShell.exe", "Unknown option \"-m\".")]
    [DataRow("run -p missing --manifest MyShell.exe", "Unknown option \"--manifest\".")]
    [DataRow("run -p missing --debug 1", "Unknown option \"--debug\".")]
    [DataRow("list -a", "Unknown option \"-a\".")]
    [DataRow("list --all", "Unknown option \"--all\".")]
    [DataRow("-V", "Unknown option \"-V\".")]
    [DataRow("list -V", "Unknown option \"-V\".")]
    [DataRow("reload --pid 0 --hard", "Unknown option \"--hard\".")]
    [DataRow("list extra", "Unexpected argument \"extra\".")]
    [DataRow("stop --pid 0 extra", "Unexpected argument \"extra\".")]
    [DataRow("run -p missing extra", "Unexpected argument \"extra\".")]
    [DataRow("doctor extra", "Unexpected argument \"extra\".")]
    [DataRow("log one two", "Unexpected argument \"two\".")]
    [DataRow("stop --pid abc", "--pid needs a whole number.")]
    [DataRow("stop --pid 1.5", "--pid needs a whole number.")]
    [DataRow("stop --pid 999999999999", "--pid needs a whole number.")]
    [DataRow("stop --pid -1", "--pid needs a whole number.")]
    [DataRow("stop --pid ''", "--pid needs a whole number.")]
    [DataRow("log --tail ten", "--tail needs a whole number.")]
    [DataRow("log --tail -5", "--tail needs a whole number.")]
    [DataRow("log -t ten", "-t needs a whole number.")]
    [DataRow("stop --pid", "--pid needs a value.")]
    [DataRow("log --tail", "--tail needs a value.")]
    [DataRow("inspect record out.mp4 --for", "--for needs a value.")]
    [DataRow("inspect record out.mp4 --for 0", "--for needs a time, such as 5s or 500ms.")]
    [DataRow("inspect record out.mp4 --for 5m", "--for needs a time, such as 5s or 500ms.")]
    [DataRow("inspect record out.mp4 --for -1s", "--for needs a time, such as 5s or 500ms.")]
    [DataRow("inspect record out.mp4 --for 1s --fps 0", "--fps needs a whole number above 0.")]
    [DataRow("inspect record out.mp4 --for 1s --fps 2.5", "--fps needs a whole number.")]
    public void A_bad_option_argument_or_number_is_named(string line, string message)
    {
        var args = line.Split(' ').Select(word => word == "''" ? "" : word).ToArray();

        var e = Assert.ThrowsExactly<ArgumentException>(() => CliParser.Parse(args));

        Assert.AreEqual(message, e.Message);
    }

    [TestMethod]
    [DataRow("run -p dir", "path", "dir")]
    [DataRow("run -s work", "shell", "work")]
    [DataRow("run -d", "daemonize", "")]
    [DataRow("run -n", "no-duplicate", "")]
    [DataRow("run --no-build", "no-build", "")]
    [DataRow("list -j", "json", "")]
    [DataRow("stop --force", "force", "")]
    [DataRow("stop -i ab", "id", "ab")]
    [DataRow("stop --pid 4", "pid", "4")]
    [DataRow("reload -s work", "shell", "work")]
    [DataRow("log -t 2", "tail", "2")]
    [DataRow("log -f", "follow", "")]
    [DataRow("ipc show -j", "json", "")]
    [DataRow("inspect tree -i ab", "id", "ab")]
    [DataRow("dev -p dir", "path", "dir")]
    [DataRow("doctor --no-color", "no-color", "")]
    [DataRow("doctor -j", "json", "")]
    public void A_command_reads_the_options_it_accepts(string line, string field, string value)
    {
        var parsed = CliParser.Parse(line.Split(' '));

        Assert.AreEqual(line.Split(' ')[0], parsed.Command);
        Assert.AreEqual(value, Field(parsed, field));
    }

    [TestMethod]
    public void An_option_before_the_command_is_that_command_s()
    {
        var parsed = CliParser.Parse(["--pid", "4", "stop"]);

        Assert.AreEqual("stop", parsed.Command);
        Assert.AreEqual("4", Field(parsed, "pid"));
    }

    [TestMethod]
    public void Every_command_takes_verbose()
    {
        foreach (var entry in CliParser.Entries)
            Assert.IsTrue(entry.Accepts(CliFlags.Verbose));
        Assert.IsTrue(CliParser.Parse(["run", "-v"]).Verbose);
    }

    private static string Field(Invocation parsed, string field)
    {
        return field switch
        {
            "path" => parsed.Text(CliFlags.Path) ?? "",
            "shell" => parsed.Text(CliFlags.Shell) ?? "",
            "id" => parsed.Text(CliFlags.Id) ?? "",
            "pid" => parsed.Number(CliFlags.Pid)?.ToString(CultureInfo.InvariantCulture) ?? "",
            "tail" => parsed.Number(CliFlags.Tail)?.ToString(CultureInfo.InvariantCulture) ?? "",
            "daemonize" => parsed.IsSet(CliFlags.Daemonize) ? "" : "no",
            "no-duplicate" => parsed.IsSet(CliFlags.NoDuplicate) ? "" : "no",
            "no-build" => parsed.IsSet(CliFlags.NoBuild) ? "" : "no",
            "json" => parsed.IsSet(CliFlags.Json) ? "" : "no",
            "force" => parsed.IsSet(CliFlags.Force) ? "" : "no",
            "follow" => parsed.IsSet(CliFlags.Follow) ? "" : "no",
            "no-color" => parsed.IsSet(CliFlags.NoColor) ? "" : "no",
            _ => throw new ArgumentException(field)
        };
    }
}
