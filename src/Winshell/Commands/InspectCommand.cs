using System.Globalization;
using System.Text.Json;
using Winshell.Capture;

namespace Winshell.Commands;

internal static class InspectCommand
{
    internal static async Task<int> RunAsync(CliOptions options)
    {
        var args = options.Positional;
        if (InstanceCall.Select(options, InstanceSelection.Question.Running, out var code) is not { } info)
            return code;
        if (args[0] == "record")
            return await RecordAsync(options, info);

        // The shell writes the file, so a relative path must mean what it means here.
        var rest = args.Skip(1).Select((arg, i) => args[0] == "screenshot" && i == 0 ? Path.GetFullPath(arg) : arg)
            .ToList();
        if (options.Right)
            rest.Insert(0, "--right");
        return await InstanceCall.PrintAsync(info, IpcRequest.ForInspect(args[0], rest));
    }

    /// <summary>
    /// The shell names the window or screen, and this process records it, so the encoding never slows the shell it
    /// watches.
    /// </summary>
    private static async Task<int> RecordAsync(CliOptions options, InstanceInfo info)
    {
        List<string> named = options.Screen is { } screen
            ? ["--screen", screen.ToString(CultureInfo.InvariantCulture)]
            : [.. options.Positional.Skip(2)];
        var answer = await InstanceCall.AskAsync(info, IpcRequest.ForInspect("record", named));
        if (answer.Response is not { Ok: true, Value: { } value })
        {
            await Console.Error.WriteLineAsync(answer.Problem(null));
            return 1;
        }

        using var json = JsonDocument.Parse(value);
        var window = (nint)json.RootElement.GetProperty("window").GetInt64();
        var target = new CaptureTarget(window, window == 0 ? json.RootElement.GetProperty("screen").GetString()! : "");
        var rate = options.Fps ?? json.RootElement.GetProperty("refreshRate").GetInt32();
        var video = Path.GetFullPath(options.Positional[1]);
        var frames = options.Frames is { } folder ? Path.GetFullPath(folder) : null;
        try
        {
            var recorded = await Recorder.RecordAsync(
                new Recording(target, video, options.For!.Value, rate, options.Fps, frames), TimeProvider.System,
                Log.Default);
            Console.WriteLine(Summary(recorded, video, frames));
            return 0;
        }
        catch (Exception e) when (CaptureFailure.IsExpected(e))
        {
            await Console.Error.WriteLineAsync($"Could not record: {e.Message}");
            return 1;
        }
    }

    private static string Summary(Recorded recorded, string video, string? frames)
    {
        var lines = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture,
                $"{recorded.Width}x{recorded.Height}, {recorded.Length.TotalSeconds:0.0##} s at {recorded.Rate} fps: {recorded.Kept} of {recorded.Captured} frames kept, {recorded.VideoFrames} in the video {video}")
        };
        if (frames is not null)
            lines.Add($"{recorded.Kept} frames and {Recorder.SheetName} in {frames}");
        if (recorded.OnScreen)
            lines.Add(
                "Windows refused to capture the window by itself, so this is its place on its screen, with anything over it.");
        if (recorded.Closed)
            lines.Add("The window or screen went away before the end.");
        return string.Join(Environment.NewLine, lines);
    }
}
