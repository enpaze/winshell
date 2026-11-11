using System.Globalization;

namespace Winshell;

/// <summary>
/// Each flag Winshell's commands share, spelled once. An entry lists the flags it takes. A flag that only one
/// subcommand takes is listed on that subcommand, not on every command.
/// </summary>
internal static class CliFlags
{
    public static readonly CommandOption Help = CommandOption.Flag(new CommandWord("--help", "-h", "/?"));

    public static readonly CommandOption Version = CommandOption.Flag(new CommandWord("--version", "-V"));

    public static readonly CommandOption Verbose = CommandOption.Flag(CommandWords.Verbose);

    public static readonly CommandOption Path = CommandOption.Value(CommandWords.Path);

    public static readonly CommandOption Config = CommandOption.Value(CommandWords.Config, ConfigName);

    public static readonly CommandOption Daemonize = CommandOption.Flag(CommandWords.Daemonize);

    public static readonly CommandOption NoDuplicate = CommandOption.Flag(CommandWords.NoDuplicate);

    public static readonly CommandOption NoBuild = CommandOption.Flag(CommandWords.NoBuild);

    public static readonly CommandOption Json = CommandOption.Flag(new CommandWord("--json", "-j"));

    public static readonly CommandOption Id = CommandOption.Value(new CommandWord("--id", "-i"));

    public static readonly CommandOption Pid = CommandOption.Value(new CommandWord("--pid"), WholeNumber);

    public static readonly CommandOption Force = CommandOption.Flag(new CommandWord("--force"));

    public static readonly CommandOption Tail = CommandOption.Value(new CommandWord("--tail", "-t"), WholeNumber);

    public static readonly CommandOption Follow = CommandOption.Flag(new CommandWord("--follow", "-f"));

    public static readonly CommandOption For = CommandOption.Value(new CommandWord("--for"), Duration);

    public static readonly CommandOption Fps = CommandOption.Value(new CommandWord("--fps"), Positive);

    public static readonly CommandOption Frames = CommandOption.Value(new CommandWord("--frames"));

    public static readonly CommandOption Screen = CommandOption.Number(new CommandWord("--screen"), ScreenNumber);

    public static readonly CommandOption Right = CommandOption.Flag(new CommandWord("--right"));

    public static readonly CommandOption Project = CommandOption.Value(new CommandWord("--project"));

    public static readonly CommandOption Yes = CommandOption.Flag(new CommandWord("--yes"));

    public static readonly CommandOption NoColor = CommandOption.Flag(new CommandWord("--no-color"));

    /// <summary>What every command takes: verbose, help and version.</summary>
    public static readonly CommandOption[] Shared = [Verbose, Help, Version];

    /// <summary>What a command that picks one instance takes.</summary>
    public static readonly CommandOption[] Instance = [Id, Pid, Path, Config];

    private static object ConfigName(string option, string name)
    {
        if (!CommandWords.IsConfigName(name))
            throw new ArgumentException(
                $"{option} needs the name of a config folder, not \"{name}\". Use -p for a path.");
        return name;
    }

    /// <summary>
    /// A process id or a tail count. A value that isn't one is an <see cref="ArgumentException"/>,
    /// which the front door turns into exit code 2.
    /// </summary>
    private static object WholeNumber(string option, string text)
    {
        // NumberStyles.Integer keeps "+5", surrounding whitespace and "-0" (which is 0). A negative is not a whole number.
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0)
            throw new ArgumentException($"{option} needs a whole number.");
        return value;
    }

    /// <summary>A frame rate: a whole number above 0.</summary>
    private static object Positive(string option, string text)
    {
        var value = (int)WholeNumber(option, text);
        if (value == 0)
            throw new ArgumentException($"{option} needs a whole number above 0.");
        return value;
    }

    /// <summary>A time: seconds, such as <c>5</c> or <c>2.5s</c>, or milliseconds, such as <c>500ms</c>. At least 1 ms.</summary>
    private static object Duration(string option, string text)
    {
        var (number, milliseconds) = text.EndsWith("ms", StringComparison.Ordinal) ? (text[..^2], 1.0)
            : text.EndsWith('s') ? (text[..^1], 1000.0)
            : (text, 1000.0);
        if (!double.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            || value * milliseconds is < 1 or > int.MaxValue)
            throw new ArgumentException($"{option} needs a time, such as 5s or 500ms.");
        return TimeSpan.FromMilliseconds(Math.Round(value * milliseconds));
    }

    /// <summary><c>--screen</c> with no number is the primary screen, numbered 0.</summary>
    private static object ScreenNumber(string option, string? text)
    {
        return text is null ? 0 : WholeNumber(option, text);
    }
}
