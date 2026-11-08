using System.Runtime.InteropServices;
using System.Threading.Channels;
using Winshell.Native;

namespace Winshell.Capture;

/// <summary>What to record, where to, and for how long.</summary>
/// <param name="Target">The window or screen.</param>
/// <param name="Video">The MP4 file to create or replace.</param>
/// <param name="Length">How long to record.</param>
/// <param name="Rate">The video's frames a second, when the encoder takes that many for the frames' size.</param>
/// <param name="Fps">The most frames to keep a second, or null to keep every frame Windows delivers.</param>
/// <param name="Frames">A new or empty folder for each kept frame as a PNG and the contact sheet, or null for none.</param>
internal sealed record Recording(
    CaptureTarget Target,
    string Video,
    TimeSpan Length,
    int Rate,
    int? Fps,
    string? Frames);

/// <summary>What a recording wrote.</summary>
/// <param name="Width">The video's width in pixels: the first frame's, rounded up to an even number.</param>
/// <param name="Height">The video's height in pixels, the same way.</param>
/// <param name="Rate">The video's frames a second: <see cref="Recording.Rate"/>, or less when the encoder refused it.</param>
/// <param name="Length">How long it recorded: the length asked for, or less when the target closed first.</param>
/// <param name="Captured">The frames Windows delivered.</param>
/// <param name="Kept">
/// The frames <see cref="Recording.Fps"/> kept, each in the frames folder. The video shows the last of those that fall in
/// one of its frames.
/// </param>
/// <param name="VideoFrames">The video's frames, with repeats.</param>
/// <param name="Closed">Whether the target closed before the end.</param>
/// <param name="OnScreen">
/// Whether Windows refused to capture the window by itself, so its place on its screen was recorded instead.
/// </param>
internal sealed record Recorded(
    int Width,
    int Height,
    int Rate,
    TimeSpan Length,
    int Captured,
    int Kept,
    long VideoFrames,
    bool Closed,
    bool OnScreen);

/// <summary>
/// <c>winshell inspect record</c>: captures a window or a screen for a while, keeps frames by <c>--fps</c>, and writes
/// them as a constant-rate MP4 and, with <c>--frames</c>, as PNGs and a contact sheet. Frames are timed from the start.
/// </summary>
internal static class Recorder
{
    /// <summary>The name of the contact sheet in the frames folder.</summary>
    public const string SheetName = "contact-sheet.png";

    private static readonly int[] FallbackRates = [120, 60, 30];

    /// <summary>
    /// Records until <see cref="Recording.Length"/> has passed on <paramref name="time"/>, or the target closes. The
    /// capture runs on Windows' thread and the encoding on the thread pool, so a slow encoder delays only the end.
    /// </summary>
    /// <exception cref="InvalidOperationException">No frame arrived, or the frames folder has files in it.</exception>
    public static async Task<Recorded> RecordAsync(Recording recording, TimeProvider time, Log log)
    {
        if (recording.Frames is { } folder)
        {
            if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
                throw new InvalidOperationException($"{folder} has files in it. --frames needs a new or empty folder.");
            Directory.CreateDirectory(folder);
        }

        var frames = Channel.CreateUnbounded<(PixelImage Frame, TimeSpan At)>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        var ended = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cap = new FrameCap(recording.Fps);
        var captured = 0;
        var start = Since(time, time.GetTimestamp());
        var recorded = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        var encoding = Task.Run(() => EncodeAsync(recording, frames.Reader, recorded.Task));
        TimeSpan length;
        bool closed;
        bool onScreen;
        using (var stream = new FrameStream(recording.Target, log, (frame, drawn) =>
               {
                   var at = drawn < start ? TimeSpan.Zero : drawn - start;
                   if (at >= recording.Length)
                       return;
                   captured++;
                   if (cap.Keep(at))
                       frames.Writer.TryWrite((frame, at));
               }, e => ended.TrySetResult(e)))
        {
            // An encoder that fails ends the recording at once, rather than after frames nobody reads pile up.
            var finished = await Task.WhenAny(Task.Delay(recording.Length, time), ended.Task, encoding);
            length = finished == ended.Task ? Since(time, time.GetTimestamp()) - start : recording.Length;
            closed = finished == ended.Task && ended.Task.Result is null;
            onScreen = stream.OnScreen;
        }

        length = Min(length, recording.Length);
        recorded.SetResult(length);
        frames.Writer.Complete();
        var (width, height, rate, kept, videoFrames) = await encoding;
        if (ended.Task is { IsCompleted: true, Result: { } failure })
            throw failure;
        if (kept == 0)
            throw new InvalidOperationException($"No frame arrived in {length.TotalSeconds:0.0} s.");
        return new Recorded(width, height, rate, length, captured, kept, videoFrames, closed, onScreen);
    }

    private static async Task<(int Width, int Height, int Rate, int Kept, long VideoFrames)> EncodeAsync(
        Recording recording, ChannelReader<(PixelImage Frame, TimeSpan At)> frames, Task<TimeSpan> length)
    {
        Mp4Writer? video = null;
        try
        {
            var width = 0;
            var height = 0;
            var rate = 0;
            var kept = 0;
            long videoFrames = 0;
            ConstantRate<PixelImage>? pace = null;
            var thumbnails = new List<(PixelImage, TimeSpan)>();
            var pngs = new List<Task>();
            var lastName = "";
            var repeats = 0;
            await foreach (var (frame, at) in frames.ReadAllAsync())
            {
                if (video is null)
                {
                    // H.264 halves the color resolution, and the encoder refuses an odd height, such as a 26 px
                    // panel's 39 at 150%. The video takes the next even size, and the extra row or column is black.
                    (width, height) = ((frame.Width + 1) & ~1, (frame.Height + 1) & ~1);
                    (video, rate) = OpenVideo(recording.Video, width, height, recording.Rate);
                    pace = new ConstantRate<PixelImage>(rate, (shown, count) =>
                    {
                        video.Write(Fitted(shown, width, height), count);
                        videoFrames += count;
                    });
                }

                kept++;
                pace!.Add(frame, at);
                if (recording.Frames is { } folder)
                {
                    thumbnails.Add((ContactSheet.Thumbnail(frame), at));
                    // Two frames can share a millisecond. The second is 000123_2.png, which sorts after the first.
                    var name = FileName(at);
                    repeats = name == lastName ? repeats + 1 : 1;
                    lastName = name;
                    var file = Path.Combine(folder, repeats == 1 ? $"{name}.png" : $"{name}_{repeats}.png");
                    pngs.Add(Task.Run(() => File.WriteAllBytesAsync(file, frame.EncodePng())));
                }
            }

            if (video is null)
                return (0, 0, 0, 0, 0);
            pace!.Finish(await length);
            video.Finish();
            if (recording.Frames is { } sheetFolder)
            {
                await Task.WhenAll(pngs);
                await File.WriteAllBytesAsync(Path.Combine(sheetFolder, SheetName),
                    ContactSheet.Render(thumbnails).EncodePng());
            }

            return (width, height, rate, kept, videoFrames);
        }
        finally
        {
            video?.Dispose();
        }
    }

    /// <summary>
    /// A video at <paramref name="rate"/> frames a second, or at the most of 120, 60 and 30 below it that the encoder
    /// takes. H.264's levels cap the frames a second by the frame size: a 250 Hz screen is over them, and the encoder
    /// picks a low level for a thin panel. The frames folder still keeps every frame.
    /// </summary>
    private static (Mp4Writer Video, int Rate) OpenVideo(string path, int width, int height, int rate)
    {
        int[] rates = [rate, .. FallbackRates.Where(lower => lower < rate)];
        for (var i = 0;; i++)
            try
            {
                return (new Mp4Writer(path, width, height, rates[i]), rates[i]);
            }
            catch (COMException) when (i < rates.Length - 1)
            {
            }
    }

    /// <summary>A kept frame's name: its milliseconds since the start, six digits or more, so the names sort in order.</summary>
    private static string FileName(TimeSpan at)
    {
        return $"{ContactSheet.Milliseconds(at):D6}";
    }

    /// <summary>
    /// <paramref name="frame"/>'s pixels in a <paramref name="width"/> by <paramref name="height"/> picture, from its
    /// top-left corner: cut or filled with black to the video's size, which is even, and stays the first frame's when a
    /// window changes size.
    /// </summary>
    private static ReadOnlySpan<byte> Fitted(PixelImage frame, int width, int height)
    {
        if (frame.Width == width && frame.Height == height)
            return frame.Bgra;
        var fitted = new byte[width * height * 4];
        var row = Math.Min(width, frame.Width) * 4;
        for (var y = 0; y < Math.Min(height, frame.Height); y++)
            frame.Bgra.Slice(y * frame.Width * 4, row).CopyTo(fitted.AsSpan(y * width * 4));
        return fitted;
    }

    /// <summary>A timestamp of <paramref name="time"/> as the time since the system started, as a capture times frames.</summary>
    private static TimeSpan Since(TimeProvider time, long timestamp)
    {
        return TimeSpan.FromTicks((long)((Int128)timestamp * TimeSpan.TicksPerSecond / time.TimestampFrequency));
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b)
    {
        return a < b ? a : b;
    }
}
