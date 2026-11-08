namespace Winshell.Tests;

/// <summary>
/// What <c>winshell inspect record</c> reads from its command line, and what it refuses before any instance is called.
/// These stay inside <see cref="CliParser"/> so a live instance on this PC is never called.
/// </summary>
public class InspectRecordOptionTests
{
    private static CliOptions Parse(string line)
    {
        return CliParser.Parse(line.Split(' '));
    }

    [Theory]
    [InlineData("5", 5000)]
    [InlineData("5s", 5000)]
    [InlineData("2.5s", 2500)]
    [InlineData("500ms", 500)]
    public void A_time_is_seconds_or_milliseconds(string time, int milliseconds)
    {
        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), Parse($"inspect record out.mp4 --for {time}").For);
    }

    [Fact]
    public void The_file_the_window_the_rate_and_the_frames_folder_are_read()
    {
        var options = Parse("inspect record out.mp4 --for 3s PanelView --fps 30 --frames shots");

        Assert.Equal(["record", "out.mp4", "PanelView"], options.Positional);
        Assert.Equal(30, options.Fps);
        Assert.Equal("shots", options.Frames);
        Assert.Null(options.Screen);
    }

    [Theory]
    [InlineData("inspect record out.mp4 --for 3s --screen", 0)]
    [InlineData("inspect record out.mp4 --screen --for 3s", 0)]
    [InlineData("inspect record out.mp4 --screen 1 --for 3s", 1)]
    public void A_screen_s_number_is_optional_and_0_without_one(string line, int screen)
    {
        var options = Parse(line);

        Assert.Equal(screen, options.Screen);
        Assert.Equal(["record", "out.mp4"], options.Positional);
    }

    [Theory]
    [InlineData("inspect record out.mp4", "winshell inspect record needs --for and a time, such as --for 5s.")]
    [InlineData("inspect record out.mov --for 1s",
        "winshell inspect record writes an MP4, so the file's name must end in .mp4.")]
    [InlineData("inspect record out.mp4 --for 1s --screen PanelView",
        "--screen records a whole screen, so it takes no window.")]
    [InlineData("inspect record out.mp4 --for 1s PanelView extra", "Unexpected argument \"extra\".")]
    public void A_recording_that_can_t_be_made_is_refused(string line, string message)
    {
        var e = Assert.Throws<ArgumentException>(() => Parse(line));

        Assert.Equal(message, e.Message);
    }
}
