using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using Winshell.Commands;

namespace Winshell;

internal static class CliParser
{
    internal static IReadOnlyList<CommandEntry> Entries => Registered;

    internal static Invocation Parse(IReadOnlyList<string> args)
    {
        var line = new Line(args);
        line.Read();
        if (line.RequestsHelp)
            return new Invocation(line.CommandName, true, false, false, [], [], []);
        if (line.CommandName is not (null or "help") && line.Version)
            return new Invocation(line.CommandName, false, true, false, [], [], []);
        return Finish(line);
    }

    internal static string Help(string? command)
    {
        var text = command is not null && ByName.TryGetValue(command, out var entry) && entry.Usage.Length > 0
            ? entry.Usage
            : Overview();
        return text.Replace("{version}", Shell.Version, StringComparison.Ordinal)
            .Replace("{shells-root}", ShellLocations.Current.ShellsRoot, StringComparison.Ordinal)
            .Replace("{shells-override}", ShellLocations.ShellsVariable, StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd();
    }

    private const string NoCommandOverview =
        "       winshell                            print this help\n" +
        "       winshell <command> --help           help for that command, which does not run";

    private const string FlagGlossary = """
                                        Shell selection:
                                          -p, --path <path>      a project file, or the folder it is in
                                          -s, --shell <name>     a named shell in {shells-root}
                                                                 ({shells-override} overrides that folder)
                                        Instance selection (kill, reload, log, ipc, inspect):
                                          -i, --id <id>          an instance id, or the start of one
                                              --pid <pid>        an instance's process id
                                        Other:
                                          -d, --daemonize        run in the background
                                          -n, --no-duplicate     exit quietly if this shell is already running
                                              --no-build         run the last build, and do not build again
                                          -v, --verbose          log debug messages
                                          -j, --json             print list, ipc show or doctor as JSON
                                              --no-color         print doctor without color
                                          -h, --help, /?         print help and exit
                                              --version          print the version
                                          --                     end options; later words are the program's arguments

                                        Exit codes: 0 done, 1 the command failed, 2 the command line was wrong.
                                        """;

    private static readonly CommandEntry HelpEntry = new()
    {
        Name = "help",
        Overview = "       winshell help [command]             print this summary, or that command's help",
        Words = WordRule.Topic
    };

    private static readonly CommandEntry[] Registered =
    [
        Share(RunCommand.Entry),
        Share(ListCommand.Entry),
        Share(KillCommand.Entry),
        Share(ReloadCommand.Entry),
        Share(DevCommand.Entry),
        Share(LogCommand.Entry),
        Share(IpcCommand.Entry),
        Share(InspectCommand.Entry),
        Share(DoctorCommand.Entry),
        Share(NewCommand.Entry),
        Share(UseCommand.Entry),
        Share(UpdateCommand.Entry),
        Share(HelpEntry)
    ];

    private static readonly FrozenDictionary<string, CommandEntry> ByName =
        Registered.ToFrozenDictionary(entry => entry.Name);

    private static readonly FrozenDictionary<string, CommandOption> ByToken = IndexTokens();

    private static CommandEntry Share(CommandEntry entry)
    {
        return entry with { Options = [.. CliFlags.Shared, .. entry.Options] };
    }

    private static string Overview()
    {
        var lines = new List<string> { "winshell {version}: a desktop shell for Windows 11", "" };
        foreach (var entry in Registered)
        {
            lines.Add(entry.Overview);
            if (entry.Name == CommandWords.Run)
                lines.Add(NoCommandOverview);
        }

        lines.Add("");
        lines.Add(FlagGlossary);
        return string.Join("\n", lines);
    }

    private static FrozenDictionary<string, CommandOption> IndexTokens()
    {
        var map = new Dictionary<string, CommandOption>(StringComparer.Ordinal);
        foreach (var entry in Registered)
        {
            foreach (var option in entry.Options)
                option.Collect(map);
            foreach (var sub in entry.Subcommands)
                foreach (var option in sub.Options)
                    option.Collect(map);
        }

        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static Invocation Finish(Line line)
    {
        var values = Apply(line);
        var positional = line.Positional;
        CommandEntry? command = null;
        CommandEntry? sub = null;
        if (line.CommandName is not { } name)
        {
            if (positional.Count > 0)
                throw Unexpected(positional[0]);
        }
        else
        {
            command = ByName[name];
            switch (command.Words)
            {
                case WordRule.None:
                    if (positional.Count > 0)
                        throw Unexpected(positional[0]);
                    break;
                case WordRule.One:
                    if (positional.Count > 1)
                        throw Unexpected(positional[1]);
                    break;
                case WordRule.Topic:
                    if (positional.Count > 1)
                        throw Unexpected(positional[1]);
                    var topic = positional.FirstOrDefault();
                    if (topic is not null && !ByName.ContainsKey(topic))
                        throw UnknownCommand(topic);
                    return new Invocation(topic, true, false, false, [], [], []);
                case WordRule.Subcommand:
                    sub = RequireSubcommand(command, positional);
                    RejectSiblingFlags(command, sub, line);
                    break;
            }
        }

        var invocation = new Invocation(
            line.CommandName,
            false,
            line.Version,
            line.CommandName is null && line.Uses.All(use => ByName[CommandWords.Run].Accepts(use.Option)),
            [.. line.Args],
            [.. positional],
            [.. line.Rest],
            values,
            sub?.Run ?? command?.Run);
        command?.Check?.Invoke(invocation);
        sub?.Check?.Invoke(invocation);

        return invocation;
    }

    private static Dictionary<CommandOption, object?> Apply(Line line)
    {
        var values = new Dictionary<CommandOption, object?>();
        ArgumentException? error = null;
        var command = line.CommandName is null ? null : ByName[line.CommandName];
        foreach (var use in line.Uses)
        {
            if (use.Option is null)
            {
                error ??= new ArgumentException($"Unknown option \"{use.Token}\".");
                continue;
            }

            if (command is not null && !command.Owns(use.Option))
            {
                error ??= new ArgumentException($"winshell {command.Name} does not take \"{use.Token}\".");
                continue;
            }

            if (use.Missing)
            {
                error ??= new ArgumentException(command?.Name == "update" && CommandWords.Channel.Is(use.Token)
                    ? "winshell update --channel needs stable or dev."
                    : $"{use.Token} needs a value.");
                continue;
            }

            try
            {
                values[use.Option] = use.Option.TakesValue || use.Option.TakesNumber
                    ? use.Option.Read(use.Token, use.Value)
                    : true;
            }
            catch (ArgumentException e)
            {
                error ??= e;
            }
        }

        if (error is not null)
            throw error;
        return values;
    }

    private static CommandEntry RequireSubcommand(CommandEntry command, List<string> positional)
    {
        if (positional.Count == 0)
            throw new ArgumentException(
                $"winshell {command.Name} needs a command. Run winshell {command.Name} --help.");
        var name = positional[0];
        var match = command.Subcommand(name);
        if (match is null)
            throw new ArgumentException(
                $"Unknown {command.Name} command \"{name}\". Run winshell {command.Name} --help.");
        if (positional.Count - 1 < match.MinWords)
            throw new ArgumentException($"winshell {command.Name} {name} needs {match.Missing}.");
        return match;
    }

    private static void RejectSiblingFlags(CommandEntry command, CommandEntry sub, Line line)
    {
        foreach (var use in line.Uses)
        {
            if (use.Option is null || command.Accepts(use.Option) || sub.Accepts(use.Option))
                continue;
            if (command.Owns(use.Option))
                throw new ArgumentException($"winshell {command.Name} {sub.Name} does not take \"{use.Token}\".");
        }
    }

    private static ArgumentException UnknownCommand(string command)
    {
        return new ArgumentException($"Unknown command \"{command}\". Run winshell --help.");
    }

    private static ArgumentException Unexpected(string argument)
    {
        return new ArgumentException($"Unexpected argument \"{argument}\".");
    }

    private readonly record struct Use(string Token, CommandOption? Option, string? Value, bool Missing);

    private sealed class Line(IReadOnlyList<string> args)
    {
        public IReadOnlyList<string> Args { get; } = args;

        public string? CommandName { get; private set; }

        public bool RequestsHelp { get; private set; }

        public bool Version { get; private set; }

        public List<Use> Uses { get; } = [];

        public List<string> Positional { get; } = [];

        public List<string> Rest { get; } = [];

        private bool _raw;

        public void Read()
        {
            for (var i = 0; i < Args.Count; i++)
            {
                var token = Args[i];
                if (_raw)
                {
                    Positional.Add(token);
                    continue;
                }

                if (token == CommandWords.End)
                {
                    if (CommandName is null or CommandWords.Run)
                    {
                        Rest.AddRange(Args.Skip(i));
                        return;
                    }

                    _raw = true;
                    continue;
                }

                if (ByToken.TryGetValue(token, out var option))
                {
                    string? value = null;
                    var missing = false;
                    if (option.TakesValue)
                    {
                        if (i + 1 >= Args.Count)
                            missing = true;
                        else
                            value = Args[++i];
                    }
                    else if (option.TakesNumber && i + 1 < Args.Count &&
                             int.TryParse(Args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    {
                        value = Args[++i];
                    }

                    Uses.Add(new Use(token, option, value, missing));
                    if (option == CliFlags.Help)
                        RequestsHelp = true;
                    else if (option == CliFlags.Version)
                        Version = true;
                    continue;
                }

                if (token.StartsWith('-') && !SignedStep(token))
                {
                    Uses.Add(new Use(token, null, null, false));
                    continue;
                }

                if (CommandName is null)
                {
                    if (!ByName.ContainsKey(token))
                        throw UnknownCommand(token);
                    CommandName = token;
                    continue;
                }

                Positional.Add(token);
                if (Sub is { RawAfter: true } sub && Positional.Count >= sub.MinWords + 1)
                    _raw = true;
            }
        }

        private CommandEntry? Sub =>
            CommandName is { } name && Positional.Count > 0 ? ByName[name].Subcommand(Positional[0]) : null;

        private bool SignedStep(string token)
        {
            return Sub is { SignedNumberIsWord: true } && int.TryParse(token, NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out _);
        }
    }
}
