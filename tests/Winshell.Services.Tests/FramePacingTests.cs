using Winshell.Capture;

namespace Winshell.Services.Tests;

/// <summary>How a recording keeps frames by <c>--fps</c>, and paces the kept ones into a constant-rate video.</summary>
public sealed class FramePacingTests
{
    private static TimeSpan Ms(double milliseconds)
    {
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    [Fact]
    public void A_cap_keeps_the_first_frame_in_each_slot_by_its_timestamp()
    {
        // 10 a second: slots of 100 ms. A long gap does not let a burst through after it.
        var cap = new FrameCap(10);
        double[] times = [0, 40, 99, 100, 150, 250, 260, 520, 530];

        var kept = times.Where(at => cap.Keep(Ms(at))).ToArray();

        Assert.Equal([0, 100, 250, 520], kept);
    }

    [Fact]
    public void Without_a_cap_every_frame_is_kept()
    {
        var cap = new FrameCap(null);

        Assert.All([0, 1, 1, 2], at => Assert.True(cap.Keep(Ms(at))));
    }

    [Fact]
    public void A_frame_repeats_until_the_next_and_only_the_last_in_a_video_frame_is_shown()
    {
        var shown = new List<(string Frame, int Count)>();
        var pace = new ConstantRate<string>(10, (frame, count) => shown.Add((frame, count)));

        pace.Add("a", Ms(30)); // the first, so it also fills the video frames before it
        pace.Add("b", Ms(250));
        pace.Add("c", Ms(260)); // in b's video frame, so it replaces b
        pace.Add("d", Ms(410));
        pace.Finish(Ms(1000));

        Assert.Equal([("a", 2), ("c", 2), ("d", 6)], shown);
    }

    [Theory]
    [InlineData(1000, 10)]
    [InlineData(1050, 11)]
    [InlineData(1, 1)]
    public void The_video_is_as_long_as_the_recording_rounded_up_to_a_whole_frame(double length, int frames)
    {
        var shown = 0;
        var pace = new ConstantRate<string>(10, (_, count) => shown += count);

        pace.Add("a", Ms(0));
        pace.Finish(Ms(length));

        Assert.Equal(frames, shown);
    }

    [Fact]
    public void A_recording_without_frames_shows_nothing()
    {
        var shown = 0;
        var pace = new ConstantRate<string>(60, (_, count) => shown += count);

        pace.Finish(Ms(1000));

        Assert.Equal(0, shown);
    }
}
