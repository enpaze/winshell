namespace Winshell.Tests;

[TestClass]
public class InspectRecordOptionTests
{
    private static Invocation Parse(string line)
    {
        return CliParser.Parse(line.Split(' '));
    }

    [TestMethod]
    [DataRow("5", 5000)]
    [DataRow("5s", 5000)]
    [DataRow("2.5s", 2500)]
    [DataRow("500ms", 500)]
    public void A_time_is_seconds_or_milliseconds(string time, int milliseconds)
    {
        Assert.AreEqual(TimeSpan.FromMilliseconds(milliseconds), Parse($"inspect record out.mp4 --for {time}").Time(CliFlags.For));
    }

    [TestMethod]
    public void The_file_the_window_the_rate_and_the_frames_folder_are_read()
    {
        var options = Parse("inspect record out.mp4 --for 3s PanelView --fps 30 --frames shots");

        Assert.AreSequenceEqual<string>(["record", "out.mp4", "PanelView"], options.Positional);
        Assert.AreEqual(30, options.Number(CliFlags.Fps));
        Assert.AreEqual("shots", options.Text(CliFlags.Frames));
        Assert.IsNull(options.Number(CliFlags.Screen));
    }

    [TestMethod]
    [DataRow("inspect record out.mp4 --for 3s --screen", 0)]
    [DataRow("inspect record out.mp4 --screen --for 3s", 0)]
    [DataRow("inspect record out.mp4 --screen 1 --for 3s", 1)]
    public void A_screen_s_number_is_optional_and_0_without_one(string line, int screen)
    {
        var options = Parse(line);

        Assert.AreEqual(screen, options.Number(CliFlags.Screen));
        Assert.AreSequenceEqual<string>(["record", "out.mp4"], options.Positional);
    }

    [TestMethod]
    [DataRow("inspect record out.mp4", "winshell inspect record needs --for and a time, such as --for 5s.")]
    [DataRow("inspect record out.mov --for 1s",
        "winshell inspect record writes an MP4, so the file's name must end in .mp4.")]
    [DataRow("inspect record out.mp4 --for 1s --screen PanelView",
        "--screen records a whole screen, so it takes no window.")]
    [DataRow("inspect record out.mp4 --for 1s PanelView extra", "Unexpected argument \"extra\".")]
    public void A_recording_that_can_t_be_made_is_refused(string line, string message)
    {
        var e = Assert.ThrowsExactly<ArgumentException>(() => Parse(line));

        Assert.AreEqual(message, e.Message);
    }
}
