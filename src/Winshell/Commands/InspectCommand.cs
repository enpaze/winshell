using System.Globalization;
using Winshell.Capture;
using Winshell.Input;

namespace Winshell.Commands;

internal static class InspectCommand
{
    /// <summary>The most wheel notches one <c>inspect scroll</c> turns.</summary>
    private const int MaxSteps = 100;

    private static readonly CommandEntry Screenshot = new()
    {
        Name = "screenshot",
        MinWords = 1,
        Missing = "a file",
        Run = ScreenshotAsync
    };

    private static readonly CommandEntry Record = new()
    {
        Name = "record",
        Options = [CliFlags.For, CliFlags.Fps, CliFlags.Frames, CliFlags.Screen],
        MinWords = 1,
        Missing = "a file",
        Check = CheckRecord,
        Run = RecordAsync
    };

    private static readonly CommandEntry Press = new()
    {
        Name = "press",
        Options = [CliFlags.Right],
        MinWords = 1,
        Missing = "a name",
        Run = PressAsync
    };

    internal static readonly CommandEntry Entry = new()
    {
        Name = "inspect",
        Overview = """
                          winshell inspect tree | screenshot <file> | record <file.mp4> --for <time>
                                           | click <name> | hover <name> | focus <name>
                                           | point <name> | leave | press <name> | scroll <name> <steps>
                                           | key <keys>
                                                              look at a running shell, or act on it
                   """,
        Usage = """
                winshell inspect [-i <id> | --pid <pid> | -c <name> | -p <path>] <command>
                  tree                            print each open window's live layout as JSON
                  screenshot <file> [window]      save a window as a PNG
                  record <file.mp4> --for <time> [window]
                                                  record a window as an MP4 at its screen's refresh
                                                  rate, or 120, 60 or 30 when the encoder takes no more
                      --screen [n]                record screen n instead, from 0, the primary one first
                      --fps <n>                   keep at most n frames a second, and make the MP4 n fps
                      --frames <folder>           also save each kept frame as a PNG named by its
                                                  milliseconds, and contact-sheet.png of them all
                  click <name> [window]           activate an element, as Narrator does
                  hover <name> [window]           put a control in its pointer look
                  Real input, which sends nothing while the PC is in use, no mouse event
                  where another window is on top, and puts the cursor back:
                  focus <name> [window]           press Tab until keyboard focus is on the element
                  point <name> [window]           keep the mouse on the element until leave, the next
                                                  of these commands, 30 seconds, or a move of the mouse
                  leave                           put the cursor back after point
                  press <name> [window] [--right] press and release a mouse button on the element
                  scroll <name> <steps> [window]  turn the mouse wheel; 3 scrolls down, -3 up
                  key <keys> [window]             press keys, such as Tab, Enter or "Ctrl+K, C". Ctrl,
                                                  Alt and Shift need the window to be in front
                  A window is the type of its content, or its number in tree. The first is the default.
                  A name is an x:Name, or an automation id or name.
                  A time is in seconds, such as 5 or 2.5s, or in milliseconds, such as 500ms.
                """,
        Options = CliFlags.Instance,
        Words = WordRule.Subcommand,
        Subcommands =
        [
            new CommandEntry { Name = "tree" },
            Screenshot,
            Record,
            new CommandEntry { Name = "click", MinWords = 1, Missing = "a name" },
            new CommandEntry { Name = "hover", MinWords = 1, Missing = "a name" },
            new CommandEntry { Name = "focus", MinWords = 1, Missing = "a name" },
            new CommandEntry { Name = "point", MinWords = 1, Missing = "a name" },
            new CommandEntry { Name = "leave" },
            Press,
            new CommandEntry
            {
                Name = "scroll",
                MinWords = 2,
                Missing = "a name and a number of steps",
                SignedNumberIsWord = true,
                Check = CheckScroll
            },
            new CommandEntry { Name = "key", MinWords = 1, Missing = "keys", Check = CheckKeys }
        ],
        Run = Execute
    };

    private static async Task<int> Execute(Invocation parsed)
    {
        if (!TrySelect(parsed, out var info, out var code))
            return code;
        var args = parsed.Positional;
        return await InstanceCall.PrintAsync(info, IpcRequest.ForInspect(args[0], args.Skip(1).ToList()));
    }

    /// <summary>The shell writes the file, so a relative path must mean what it means here.</summary>
    private static async Task<int> ScreenshotAsync(Invocation parsed)
    {
        if (!TrySelect(parsed, out var info, out var code))
            return code;
        var rest = parsed.Positional.Skip(1).Select((arg, i) => i == 0 ? Path.GetFullPath(arg) : arg).ToList();
        return await InstanceCall.PrintAsync(info, IpcRequest.ForInspect("screenshot", rest));
    }

    private static async Task<int> PressAsync(Invocation parsed)
    {
        if (!TrySelect(parsed, out var info, out var code))
            return code;
        var rest = parsed.Positional.Skip(1).ToList();
        if (parsed.IsSet(CliFlags.Right))
            rest.Insert(0, "--right");
        return await InstanceCall.PrintAsync(info, IpcRequest.ForInspect("press", rest));
    }

    private static bool TrySelect(Invocation parsed, out InstanceInfo info, out int code)
    {
        var selected = InstanceCall.Select(parsed.Instance, InstanceSelection.Question.Running, out code);
        info = selected!;
        return selected is not null;
    }

    /// <summary>
    /// <c>inspect record</c> needs a time and an MP4, and records a window or a screen, not both.
    /// </summary>
    private static void CheckRecord(Invocation parsed)
    {
        if (parsed.Time(CliFlags.For) is null)
            throw new ArgumentException("winshell inspect record needs --for and a time, such as --for 5s.");
        if (!parsed.Positional[1].EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("winshell inspect record writes an MP4, so the file's name must end in .mp4.");
        if (parsed.Number(CliFlags.Screen) is not null && parsed.Positional.Length > 2)
            throw new ArgumentException("--screen records a whole screen, so it takes no window.");
        if (parsed.Positional.Length > 3)
            throw new ArgumentException($"Unexpected argument \"{parsed.Positional[3]}\".");
    }

    private static void CheckScroll(Invocation parsed)
    {
        if (!int.TryParse(parsed.Positional[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                out var steps) || steps == 0 || Math.Abs(steps) > MaxSteps)
            throw new ArgumentException(
                $"winshell inspect scroll needs a number of steps from 1 to {MaxSteps}, or -1 to -{MaxSteps} to scroll up.");
    }

    /// <summary>Keys <c>inspect key</c> can press: chords as a hotkey writes them, without the Windows key.</summary>
    private static void CheckKeys(Invocation parsed)
    {
        var keys = parsed.Positional[1];
        if (!HotkeySequence.TryParse(keys, out var sequence, out var error))
            throw new ArgumentException(error ?? "winshell inspect key needs keys, such as Tab or Ctrl+S.");
        if (sequence!.Chords.FirstOrDefault(chord => (chord.Modifiers & HotkeyModifiers.Win) != 0) is { } win)
            throw new ArgumentException(
                $"winshell inspect key does not press {win.Text}: Windows itself takes the Windows key, not the shell.");
    }

    /// <summary>
    /// The shell names the window or screen, and this process records it, so the encoding never slows the shell it
    /// watches.
    /// </summary>
    private static async Task<int> RecordAsync(Invocation parsed)
    {
        if (!TrySelect(parsed, out var info, out var code))
            return code;
        List<string> named = parsed.Number(CliFlags.Screen) is { } screen
            ? ["--screen", screen.ToString(CultureInfo.InvariantCulture)]
            : [.. parsed.Positional.Skip(2)];
        var answer = await InstanceCall.AskAsync(info, IpcRequest.ForInspect("record", named));
        if (answer.Response is not { Ok: true, Value: { } value })
        {
            await Console.Error.WriteLineAsync(answer.Problem(null));
            return 1;
        }

        var recordTarget = InspectRecordTarget.Read(value);
        var window = (nint)recordTarget.Window;
        var target = new CaptureTarget(window, window == 0 ? recordTarget.Screen : "");
        var rate = parsed.Number(CliFlags.Fps) ?? recordTarget.RefreshRate;
        var video = Path.GetFullPath(parsed.Positional[1]);
        var frames = parsed.Text(CliFlags.Frames) is { } folder ? Path.GetFullPath(folder) : null;
        try
        {
            var recorded = await Recorder.RecordAsync(
                new Recording(target, video, parsed.Time(CliFlags.For)!.Value, rate, parsed.Number(CliFlags.Fps), frames),
                TimeProvider.System,
                Log.Default);
            Console.WriteLine(Summary(recorded, video, frames));
            return 0;
        }
        catch (Exception e) when (WindowsFailure.IsExpected(e))
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
