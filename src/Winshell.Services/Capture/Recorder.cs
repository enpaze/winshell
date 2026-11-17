using System.Runtime.InteropServices;
using System.Threading.Channels;
using Winshell.Native;

namespace Winshell.Capture;

internal sealed record Recording(
    CaptureTarget Target,
    string Video,
    TimeSpan Length,
    int Rate,
    int? Fps,
    string? Frames);

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

internal static class Recorder
{
    public const string SheetName = "contact-sheet.png";

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
            using var timer = new CancellationTokenSource();
            var finished = await Task.WhenAny(Task.Delay(recording.Length, time, timer.Token), ended.Task, encoding);
            await timer.CancelAsync();
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
                    var videoWidth = (frame.Width + 1) & ~1;
                    var videoHeight = (frame.Height + 1) & ~1;
                    var (writer, openedRate) = OpenVideo(recording.Video, videoWidth, videoHeight, recording.Rate);
                    video = writer;
                    width = videoWidth;
                    height = videoHeight;
                    rate = openedRate;
                    pace = new ConstantRate<PixelImage>(rate, (shown, count) =>
                    {
                        writer.Write(Fitted(shown, videoWidth, videoHeight), count);
                        videoFrames += count;
                    });
                }

                kept++;
                pace!.Add(frame, at);
                if (recording.Frames is { } folder)
                {
                    thumbnails.Add((ContactSheet.Thumbnail(frame), at));
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

    private static (Mp4Writer Video, int Rate) OpenVideo(string path, int width, int height, int rate)
    {
        List<int> rates = [rate];
        if (rate > 120)
            rates.Add(120);
        if (rate > 60)
            rates.Add(60);
        if (rate > 30)
            rates.Add(30);
        for (var i = 0; i < rates.Count - 1; i++)
            if (TryOpen(path, width, height, rates[i]) is { } opened)
                return opened;
        return (new Mp4Writer(path, width, height, rates[^1]), rates[^1]);
    }

    private static (Mp4Writer Video, int Rate)? TryOpen(string path, int width, int height, int rate)
    {
        try
        {
            return (new Mp4Writer(path, width, height, rate), rate);
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static string FileName(TimeSpan at)
    {
        return $"{ContactSheet.Milliseconds(at):D6}";
    }

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

    private static TimeSpan Since(TimeProvider time, long timestamp)
    {
        return TimeSpan.FromTicks((long)((Int128)timestamp * TimeSpan.TicksPerSecond / time.TimestampFrequency));
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b)
    {
        return a < b ? a : b;
    }
}
