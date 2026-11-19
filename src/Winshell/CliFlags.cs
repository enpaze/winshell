using System.Globalization;

namespace Winshell;

internal static class CliFlags
{
    public static readonly CommandOption Help = CommandOption.Flag(new CommandWord("--help", "-h", "/?"));

    public static readonly CommandOption Version = CommandOption.Flag(new CommandWord("--version"));

    public static readonly CommandOption Verbose = CommandOption.Flag(CommandWords.Verbose);

    public static readonly CommandOption Path = CommandOption.Value(CommandWords.Path);

    public static readonly CommandOption Shell = CommandOption.Value(CommandWords.Shell, ShellFolder);

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

    public static readonly CommandOption NoColor = CommandOption.Flag(new CommandWord("--no-color"));

    public static readonly CommandOption[] Shared = [Verbose, Help, Version];

    public static readonly CommandOption[] Instance = [Id, Pid, Path, Shell];

    private static object ShellFolder(string option, string name)
    {
        if (!CommandWords.IsShellFolder(name))
            throw new ArgumentException(
                $"{option} needs the name of a shell folder, not \"{name}\". Use -p for a path.");
        return name;
    }

    private static object WholeNumber(string option, string text)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0)
            throw new ArgumentException($"{option} needs a whole number.");
        return value;
    }

    private static object Positive(string option, string text)
    {
        var value = (int)WholeNumber(option, text);
        if (value == 0)
            throw new ArgumentException($"{option} needs a whole number above 0.");
        return value;
    }

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

    private static object ScreenNumber(string option, string? text)
    {
        return text is null ? 0 : WholeNumber(option, text);
    }
}
