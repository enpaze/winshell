using System.Globalization;
using System.Text;
using Winshell.Session;
using WindowsCommand = Winshell.Native.CommandLine;

namespace Winshell;

/// <summary>
/// A Winshell command line. Parsing, quoting and the sign-in policy live here, so the sign-in entry, a shell's
/// restart and an app launch cannot each grow their own.
/// </summary>
internal sealed class CommandLine
{
    private const string ConsoleHost = "conhost.exe";

    private const string Headless = "--headless";

    private readonly string[] _arguments;

    private CommandLine(string[] arguments) => _arguments = arguments;

    public IReadOnlyList<string> Arguments => _arguments;

    /// <summary>The arguments of a Windows command line. A blank line has none: <c>CommandLineToArgvW</c> would return this program's path for one.</summary>
    public static CommandLine Parse(string command) => new(WindowsCommand.Split(command));

    public static CommandLine Parse(IEnumerable<string> arguments) => new(arguments.ToArray());

    /// <summary>Quotes an argument the way <c>CommandLineToArgvW</c> reads it back.</summary>
    public static string Quote(string argument)
    {
        if (argument.Length > 0 && !argument.Any(static c => c is ' ' or '\t' or '"'))
            return argument;
        var written = new StringBuilder("\"");
        var slashes = 0;
        foreach (var c in argument)
        {
            if (c == '\\')
            {
                slashes++;
                continue;
            }

            written.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
            slashes = 0;
            written.Append(c);
        }

        written.Append('\\', slashes * 2).Append('"');
        return written.ToString();
    }

    /// <summary>A program and arguments Windows will parse. <paramref name="arguments"/> is already a command line.</summary>
    public static string Join(string program, string arguments)
    {
        var quoted = Quote(program);
        return arguments.Length == 0 ? quoted : quoted + " " + arguments;
    }

    /// <summary>What <paramref name="command"/> starts. A blank command starts nothing.</summary>
    public static SignInEntry.SignInLaunch ReadSignIn(string command) => Parse(command).ReadSignIn();

    /// <summary>The command a sign-in runs for <paramref name="exe"/> with <paramref name="arguments"/>.</summary>
    public static string ForSignIn(string exe, IEnumerable<string> arguments) => Parse(arguments).ForSignIn(exe);

    /// <summary>What a configuration's program reads from <paramref name="arguments"/>.</summary>
    public static ShellArguments ReadProgram(IEnumerable<string> arguments) => Parse(arguments).ReadProgram();

    /// <summary>
    /// The words <c>winshell run</c> gives a configuration's program. <paramref name="rest"/> is the <c>--</c> and the
    /// words after it.
    /// </summary>
    public static IReadOnlyList<string> ProgramArguments(bool noDuplicate, bool verbose, IEnumerable<string> rest)
    {
        var words = new List<string>();
        if (noDuplicate)
            words.Add(CommandWords.NoDuplicate.Long);
        if (verbose)
            words.Add(CommandWords.Verbose.Long);
        words.AddRange(rest);
        return words;
    }

    public SignInEntry.SignInLaunch ReadSignIn()
    {
        if (_arguments.Length == 0)
            return default;
        int start;
        string program;
        if (_arguments.Length >= 3 &&
            Path.GetFileName(_arguments[0]).Equals(ConsoleHost, StringComparison.OrdinalIgnoreCase) &&
            _arguments[1] == Headless)
        {
            program = _arguments[2];
            start = 3;
        }
        else
        {
            program = _arguments[0];
            start = 1;
        }

        string? path = null;
        string? name = null;
        for (var i = start; i < _arguments.Length; i++)
        {
            if (_arguments[i] == CommandWords.End)
                break;
            if (CommandWords.Path.Is(_arguments[i]) && i + 1 < _arguments.Length)
                path = _arguments[++i];
            else if (CommandWords.Config.Is(_arguments[i]) && i + 1 < _arguments.Length)
                name = _arguments[++i];
        }

        return new SignInEntry.SignInLaunch(program, path, name);
    }

    /// <summary>
    /// The command a sign-in runs: this line's arguments with <paramref name="exe"/>, under a console host that has no
    /// window. <c>winshell.exe</c> is a console program, so started on its own it would open a console or Windows
    /// Terminal at every sign-in. Start launches <c>winshellw.exe</c>; that name is written as the <c>winshell.exe</c>
    /// beside it, so the Start menu and the installer describe one entry. <c>--no-duplicate</c> keeps a second sign-in
    /// entry from starting a second shell, and <c>--daemonize</c> is left out because the console host already runs it
    /// in the background. <c>--no-build</c> runs the configuration's last build, so signing in never depends on the SDK
    /// or on a broken edit. A <c>--path</c> is made absolute, since a sign-in does not start in this working directory.
    /// The <c>--</c> and the words after it are the program's, and stay last as they are.
    /// </summary>
    public string ForSignIn(string exe)
    {
        var end = Array.IndexOf(_arguments, CommandWords.End);
        var words = end < 0 ? _arguments : _arguments[..end];
        var kept = new List<string>();
        for (var i = 0; i < words.Length; i++)
        {
            if (CommandWords.Daemonize.Is(words[i]))
                continue;
            kept.Add(words[i]);
            if (i + 1 < words.Length && CommandWords.Path.Is(words[i]))
                kept.Add(Path.GetFullPath(words[++i]));
            else if (i + 1 < words.Length && CommandWords.Config.Is(words[i]))
                kept.Add(words[++i]);
        }

        if (!kept.Any(CommandWords.NoBuild.Is))
        {
            var duplicate = kept.FindIndex(CommandWords.NoDuplicate.Is);
            kept.Insert(duplicate >= 0 ? duplicate : kept.Count, CommandWords.NoBuild.Long);
        }

        if (!kept.Any(CommandWords.NoDuplicate.Is))
            kept.Add(CommandWords.NoDuplicate.Long);
        if (end >= 0)
            kept.AddRange(_arguments.Skip(end));
        var conhost = Path.Combine(Environment.SystemDirectory, ConsoleHost);
        return string.Join(' ', new[] { conhost, Headless, WinshellHost.ConsoleOf(exe) }.Concat(kept).Select(Quote));
    }

    public ShellArguments ReadProgram()
    {
        int? after = null;
        var noDuplicate = false;
        var daemonize = false;
        var verbose = false;
        var kept = new List<string>();
        var detached = new List<string>();
        for (var i = 0; i < _arguments.Length; i++)
        {
            if (_arguments[i] == CommandWords.End)
            {
                kept.AddRange(_arguments.Skip(i));
                detached.AddRange(_arguments.Skip(i));
                break;
            }

            if (CommandWords.After.Is(_arguments[i]) && i + 1 < _arguments.Length &&
                int.TryParse(_arguments[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
            {
                after = pid;
                detached.Add(_arguments[i]);
                detached.Add(_arguments[i + 1]);
                i++;
                continue;
            }

            if (CommandWords.Daemonize.Is(_arguments[i]))
            {
                daemonize = true;
                continue;
            }

            noDuplicate |= CommandWords.NoDuplicate.Is(_arguments[i]);
            verbose |= CommandWords.Verbose.Is(_arguments[i]);
            kept.Add(_arguments[i]);
            detached.Add(_arguments[i]);
        }

        return new ShellArguments(after, noDuplicate, daemonize, kept, detached, verbose);
    }

    /// <summary>Whether these arguments are <paramref name="other"/>'s, reading <c>winshellw.exe</c> as <c>winshell.exe</c>.</summary>
    public bool SameProgram(CommandLine other)
    {
        return _arguments.Select(WinshellHost.ConsoleOf).SequenceEqual(
            other._arguments.Select(WinshellHost.ConsoleOf), StringComparer.OrdinalIgnoreCase);
    }
}
