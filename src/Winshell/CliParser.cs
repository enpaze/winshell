using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;

namespace Winshell;

/// <summary>
/// One table says what every command accepts. <see cref="Parse"/> reads the arguments once, and <see cref="Help"/>
/// prints the same table.
/// </summary>
internal static class CliParser
{
    internal static CliOptions Parse(string[] args)
    {
        var line = new Line(args);
        line.Read();
        if (line.RequestsHelp)
            return new CliOptions { Command = line.CommandName, ShowHelp = true };
        if (line.CommandName is not (null or "help") && line.Version)
            return new CliOptions { Command = line.CommandName, ShowVersion = true };
        return Finish(line);
    }

    /// <summary>The help for <paramref name="command"/>, or the summary when that is null or has no help of its own.</summary>
    internal static string Help(string? command)
    {
        var text = command is not null && Commands.TryGetValue(command, out var entry) ? entry.Usage : Summary;
        return text.Replace("{version}", Shell.Version, StringComparison.Ordinal)
            .Replace("{config-root}", ShellLocations.Current.ConfigRoot, StringComparison.Ordinal)
            .Replace("{config-override}", ShellLocations.ConfigVariable, StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd();
    }

    private static readonly Option HelpOption = Option.Flag(new CommandWord("--help", "-h", "/?"), o => o);

    private static readonly Option VersionOption = Option.Flag(new CommandWord("--version", "-V"), o => o);

    private static readonly Option[] Shared =
    [
        Option.Flag(CommandWords.Verbose, o => o with { Verbose = true }), HelpOption, VersionOption
    ];

    private static readonly Option PathOption =
        Option.Value(CommandWords.Path, (o, use) => o with { Path = use.Value });

    private static readonly Option ConfigOption = Option.Value(CommandWords.Config,
        (o, use) => o with { Config = ConfigName(use.Token, use.Value!) });

    private static readonly Option DaemonizeOption =
        Option.Flag(CommandWords.Daemonize, o => o with { Daemonize = true });

    private static readonly Option JsonOption =
        Option.Flag(new CommandWord("--json", "-j"), o => o with { Json = true });

    private static readonly Option[] Instance =
    [
        Option.Value(new CommandWord("--id", "-i"), (o, use) => o with { Id = use.Value }),
        Option.Value(new CommandWord("--pid"), (o, use) => o with { Pid = WholeNumber(use.Token, use.Value!) }),
        PathOption, ConfigOption
    ];

    /// <summary>What only <c>inspect record</c> takes.</summary>
    private static readonly Option[] Record =
    [
        Option.Value(new CommandWord("--for"), (o, use) => o with { For = Duration(use.Token, use.Value!) }),
        Option.Value(new CommandWord("--fps"), (o, use) => o with { Fps = Positive(use.Token, use.Value!) }),
        Option.Value(new CommandWord("--frames"), (o, use) => o with { Frames = use.Value }),
        Option.Number(new CommandWord("--screen"),
            (o, use) => o with { Screen = use.Value is null ? 0 : WholeNumber(use.Token, use.Value) })
    ];

    /// <summary>What only <c>inspect press</c> takes.</summary>
    private static readonly Option RightOption = Option.Flag(new CommandWord("--right"), o => o with { Right = true });

    private static readonly Option ProjectOption =
        Option.Value(new CommandWord("--project"), (o, use) => o with { Project = use.Value });

    private static readonly Option YesOption = Option.Flag(new CommandWord("--yes"), o => o with { Yes = true });

    private const string RunUsage = """
                                    winshell run [-c <name> | -p <path>] [-d] [-n] [--no-build] [-- <args>...]
                                      Starts a configuration project's shell, built first when a source is newer than its
                                      program. With no project, nothing starts.
                                      -d, --daemonize        run in the background
                                      -n, --no-duplicate     exit quietly if this config is already running
                                          --no-build         run the last build, and do not build again
                                      Words after -- go to the configuration's program.
                                    """;

    private const string ListUsage = """
                                     winshell list [-j]
                                       Lists running instances. -j, --json prints them as JSON.
                                       Stopped instances are not listed.
                                     """;

    private const string KillUsage = """
                                     winshell kill [--force] [-i <id> | --pid <pid> | -c <name> | -p <path>]
                                       Asks one running instance to exit, then waits until that process has exited.
                                       --force ends the process when it does not exit.
                                       With one running instance and no selector, that instance is used.
                                       With several, this exits and lists them.
                                     """;

    private const string ReloadUsage = """
                                       winshell reload [-i <id> | --pid <pid> | -c <name> | -p <path>]
                                         Builds a configuration project and asks its instance to start again.
                                         A build that fails leaves the instance running and asks it to show the error.
                                         With one running instance and no selector, that instance is used.
                                         With several, this exits and lists them.
                                       """;

    private const string InspectUsage = """
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
                                        """;

    private const string DevUsage = """
                                    winshell dev [-c <name> | -p <path>]
                                      Runs a configuration project and applies each edit to the running shell, until Ctrl+C.
                                      A saved C# or XAML file reaches the shell in place, and the other windows are left alone.
                                      What does not compile shows on screen, and the last version that did keeps running.
                                      Needs the .NET SDK. See the guide for what an edit can change.
                                    """;

    private const string LogUsage = """
                                    winshell log [-i <id> | --pid <pid> | -c <name> | -p <path>] [-t <n>] [-f] [file]
                                      Prints an instance's log. -t, --tail <n> prints the last n lines.
                                      -f, --follow keeps printing new lines. winshell log <file> prints that file.
                                      With one running instance and no selector, that instance is used.
                                      With several, this exits and lists them.
                                      With nothing running and no selector, the log of a stopped instance is printed.
                                    """;

    private const string IpcUsage = """
                                    winshell ipc [-i <id> | --pid <pid> | -c <name> | -p <path>] <command>
                                      show [-j]                             list IPC targets. -j prints JSON
                                      call <target> <function> [args...]    call a function and print the result
                                      call notice dismiss                   close Winshell's own notice
                                      prop get <target> <property>          print a property
                                      wait <target> <signal>                wait for one signal
                                      listen <target> <signal>              print each signal until Ctrl+C
                                      Words after the function name are arguments, even when they start with -.
                                      A -- ends options on every command.
                                    """;

    private const string DoctorUsage = """
                                       winshell doctor [-c <name> | -p <path>] [-j] [--no-color]
                                         Checks this PC's setup for Winshell and changes nothing.
                                         -c, --config <name>    a named config in {config-root}
                                         -j, --json             print the checks as JSON
                                             --no-color         print without color
                                       """;

    private const string WidgetUsage = """
                                       winshell widget [-c <name> | -p <path>] <command>
                                         install <source>[@ref] [--project <path>] [--yes]
                                           Installs widgets from github.com/owner/repo, an https://github.com URL, a file:// URL or a folder.
                                           @ref is a branch, a tag or a commit. With none, the default branch is used.
                                           The command resolves it to a commit, shows the repository, the commit and the
                                           widget names, and asks. --yes installs without asking.
                                           Building runs this repository's code and MSBuild targets in a program that starts at sign-in.
                                           Declining the question changes nothing. Needs git and the .NET SDK.
                                         list
                                           Lists each installed widget's name, source, ref and commit.
                                         remove <name>
                                           Removes a widget, or a source, and every widget from that repository.
                                         update [<name>] [--yes]
                                           Fetches the recorded ref of every installed source, or of the one <name> selects.
                                           <name> is a widget name or the source. A ref that still names the recorded
                                           commit is up to date. Otherwise the command shows the old and new commit,
                                           the commits between them, and the widget names that would be added or dropped,
                                           and asks. --yes updates without asking.
                                           Building runs this repository's code and MSBuild targets in a program that starts at sign-in.
                                           Declining leaves that source as it is. A failed build keeps the previous commit,
                                           and older commits stay in the cache. Needs git and the .NET SDK.
                                         --project <path>    the project in the repository that lists the widgets
                                         -c and -p choose the configuration, as they do for winshell run.
                                       """;

    private const string NewUsage = """
                                    winshell new [<name>] [-p <folder>]
                                      With no name, lists the starter and the examples this release can copy.
                                      With a name, copies that one into {config-root}\<name>, ready for
                                      winshell run -c <name>. Its project references this release's Winshell package.
                                      -p, --path <folder>    copy into this folder instead
                                      A folder that exists and isn't empty is refused, and nothing changes.
                                    """;

    private const string UseUsage = """
                                    winshell use [<name>]
                                      With a name, builds that config in {config-root} when it needs a build, and
                                      chooses it: sign-in, the Start tile and winshell run with no -c or -p start it.
                                      A shell that is running stops, and the chosen one starts in its place.
                                      A failed build changes nothing.
                                      With no name, lists the configs, with * before the one winshell run starts.
                                    """;

    private const string Summary = """
                                   winshell {version}: a desktop shell for Windows 11

                                   Usage: winshell run [options]              start a shell
                                          winshell                            print this help
                                          winshell <command> --help           help for that command, which does not run
                                          winshell list [-j]                  list running instances
                                          winshell kill [--force] [instance options]
                                                                              stop an instance and wait until it exits
                                          winshell reload [instance options]
                                                                              reload an instance's configuration
                                          winshell dev [-c <name> | -p <path>]
                                                                              run a project and apply each edit to it
                                          winshell log [-f] [-t N] [file]     print an instance's log
                                          winshell ipc show [-j]              list IPC targets
                                          winshell ipc call <target> <function> [args...]
                                          winshell ipc prop get <target> <property>
                                          winshell ipc wait <target> <signal>
                                          winshell ipc listen <target> <signal>
                                          winshell inspect tree | screenshot <file> | record <file.mp4> --for <time>
                                                           | click <name> | hover <name> | focus <name>
                                                           | point <name> | leave | press <name> | scroll <name> <steps>
                                                           | key <keys>
                                                                              look at a running shell, or act on it
                                          winshell doctor [config options] [-j] [--no-color]
                                                                              check this PC's setup for Winshell
                                          winshell new [<name>] [-p <folder>] list the examples, or copy one to start from
                                          winshell use [<name>]               choose the config sign-in starts, and switch to it
                                          winshell widget install <source>[@ref] [--yes]
                                                                              install widgets from a GitHub repository
                                          winshell widget list                list the widgets a configuration has
                                          winshell widget remove <name>       remove an installed widget
                                          winshell widget update [<name>] [--yes]
                                                                              move installed widgets to the ref's new commit
                                          winshell help [command]             print this summary, or that command's help

                                   Config selection:
                                     -p, --path <path>      a project file, or the folder it is in
                                     -c, --config <name>    a named config in {config-root}
                                                            ({config-override} overrides that folder)
                                   Instance selection (kill, reload, log, ipc, inspect):
                                     -i, --id <id>          an instance id, or the start of one
                                         --pid <pid>        an instance's process id
                                   Other:
                                     -d, --daemonize        run in the background
                                     -n, --no-duplicate     exit quietly if this config is already running
                                         --no-build         run the last build, and do not build again
                                     -v, --verbose          log debug messages
                                     -j, --json             print list, ipc show or doctor as JSON
                                         --no-color         print doctor without color
                                     -h, --help, /?         print help and exit
                                     -V, --version          print the version
                                     --                     end options; later words are the program's arguments

                                   Exit codes: 0 done, 1 the command failed, 2 the command line was wrong.
                                   """;

    /// <summary>Every command, the options and words it takes, and the help it prints. Each also takes <see cref="Shared"/>.</summary>
    private static readonly Command[] Entries =
    [
        Entry(CommandWords.Run, RunUsage, WordRule.None,
        [
            PathOption, ConfigOption, DaemonizeOption,
            Option.Flag(CommandWords.NoDuplicate, o => o with { NoDuplicate = true }),
            Option.Flag(CommandWords.NoBuild, o => o with { NoBuild = true })
        ]),
        Entry("list", ListUsage, WordRule.None, [JsonOption]),
        Entry("kill", KillUsage, WordRule.None,
            [.. Instance, Option.Flag(new CommandWord("--force"), o => o with { Force = true })]),
        Entry("reload", ReloadUsage, WordRule.None, Instance),
        Entry("log", LogUsage, WordRule.One,
        [
            .. Instance,
            Option.Value(new CommandWord("--tail", "-t"),
                (o, use) => o with { Tail = WholeNumber(use.Token, use.Value!) }),
            Option.Flag(new CommandWord("--follow", "-f"), o => o with { Follow = true })
        ]),
        Entry("ipc", IpcUsage, WordRule.Subcommand, [.. Instance, JsonOption],
        [
            new Subcommand(IpcOps.Show),
            new Subcommand(IpcOps.Call, 2, "a target and a function", RawAfter: true),
            new Subcommand(IpcOps.Prop, 3, "\"get\", a target and a property", "get"),
            new Subcommand(IpcOps.Wait, 2, "a target and a signal"),
            new Subcommand(IpcOps.Listen, 2, "a target and a signal")
        ]),
        Entry("inspect", InspectUsage, WordRule.Subcommand, [.. Instance, .. Record, RightOption],
        [
            new Subcommand("tree"),
            new Subcommand("screenshot", 1, "a file"),
            new Subcommand("record", 1, "a file"),
            new Subcommand("click", 1, "a name"),
            new Subcommand("hover", 1, "a name"),
            new Subcommand("focus", 1, "a name"),
            new Subcommand("point", 1, "a name"),
            new Subcommand("leave"),
            new Subcommand("press", 1, "a name"),
            new Subcommand("scroll", 2, "a name and a number of steps"),
            new Subcommand("key", 1, "keys")
        ]),
        Entry("dev", DevUsage, WordRule.None, [PathOption, ConfigOption]),
        Entry("doctor", DoctorUsage, WordRule.None,
        [
            PathOption, ConfigOption, JsonOption,
            Option.Flag(new CommandWord("--no-color"), o => o with { NoColor = true })
        ]),
        Entry("new", NewUsage, WordRule.One, [PathOption]),
        Entry("use", UseUsage, WordRule.One, []),
        Entry("widget", WidgetUsage, WordRule.Subcommand, [PathOption, ConfigOption, ProjectOption, YesOption],
        [
            new Subcommand("install", 1, "a source", AtMost: 1),
            new Subcommand("list", AtMost: 0),
            new Subcommand("remove", 1, "a name", AtMost: 1),
            new Subcommand("update", AtMost: 1)
        ]),
        Entry("help", Summary, WordRule.Topic, [])
    ];

    private static readonly FrozenDictionary<string, Command>
        Commands = Entries.ToFrozenDictionary(entry => entry.Name);

    /// <summary>
    /// <c>run</c>. A line with no command word and only its options starts a shell, as the Start tile and sign-in do.
    /// </summary>
    private static readonly Command RunEntry = Commands[CommandWords.Run];

    private static readonly FrozenDictionary<string, Option> ByToken = IndexTokens();

    private static FrozenDictionary<string, Option> IndexTokens()
    {
        var map = new Dictionary<string, Option>(StringComparer.Ordinal);
        foreach (var entry in Entries)
            foreach (var option in entry.Options)
                option.Collect(map);
        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static Command Entry(string name, string usage, WordRule words, Option[] options,
        Subcommand[]? subcommands = null)
    {
        return new Command(name, usage, [.. Shared, .. options], subcommands ?? [], words);
    }

    private static CliOptions Finish(Line line)
    {
        var options = Apply(line);
        var positional = line.Positional;
        if (line.CommandName is not { } name)
        {
            if (positional.Count > 0)
                throw Unexpected(positional[0]);
        }
        else
        {
            var command = Commands[name];
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
                    if (topic is not null && !Commands.ContainsKey(topic))
                        throw UnknownCommand(topic);
                    return new CliOptions { Command = topic, ShowHelp = true };
                case WordRule.Subcommand:
                    RequireSubcommand(command, positional);
                    if (name == "inspect")
                        CheckInspect(line, options, positional);
                    else if (name == "widget")
                        CheckWidget(line, positional);
                    break;
            }

            if (name == "use" && positional.Count == 1 && !CommandWords.IsConfigName(positional[0]))
                throw new ArgumentException($"winshell use needs the name of a config folder, not \"{positional[0]}\".");
        }

        return options with
        {
            Command = line.CommandName,
            Arguments = [.. line.Args],
            ShowVersion = line.Version,
            ShellStartAllowed = line.CommandName is null && line.Uses.All(use => RunEntry.Accepts(use.Option)),
            Positional = [.. positional],
            Rest = [.. line.Rest],
            ChildArguments = BackgroundChild(line, options)
        };
    }

    /// <summary>
    /// What a <c>-d</c> child runs. The flag itself is gone. <c>--no-build</c> is added when this process builds,
    /// so the child does not build again. A value spelled <c>-d</c> stays.
    /// </summary>
    private static ImmutableArray<string> BackgroundChild(Line line, CliOptions options)
    {
        if (!options.Daemonize)
            return [];
        var child = line.Child.ToList();
        if (line.CommandName is null)
            child.Insert(0, CommandWords.Run);
        if (!options.NoBuild)
            child.Insert(0, CommandWords.NoBuild.Long);
        return [.. child];
    }

    private static CliOptions Apply(Line line)
    {
        var options = new CliOptions();
        ArgumentException? error = null;
        var command = line.CommandName is null ? null : Commands[line.CommandName];
        foreach (var use in line.Uses)
        {
            if (use.Option is null)
            {
                error ??= new ArgumentException($"Unknown option \"{use.Token}\".");
                continue;
            }

            if (command is not null && !command.Accepts(use.Option))
            {
                error ??= new ArgumentException($"winshell {command.Name} does not take \"{use.Token}\".");
                continue;
            }

            if (use.Missing)
            {
                error ??= new ArgumentException($"{use.Token} needs a value.");
                continue;
            }

            try
            {
                options = use.Option.Set(options, use);
            }
            catch (ArgumentException e)
            {
                error ??= e;
            }
        }

        if (error is not null)
            throw error;
        return options;
    }

    private static void RequireSubcommand(Command command, List<string> positional)
    {
        if (positional.Count == 0)
            throw new ArgumentException(
                $"winshell {command.Name} needs a command. Run winshell {command.Name} --help.");
        var name = positional[0];
        var match = command.Subcommands.FirstOrDefault(sub => sub.Name == name);
        if (match.Name is null)
            throw new ArgumentException(
                $"Unknown {command.Name} command \"{name}\". Run winshell {command.Name} --help.");
        var words = positional.Count - 1;
        var wrongWord = match.Word is { } required && (words < 1 || positional[1] != required);
        if (wrongWord || words < match.Words)
            throw new ArgumentException($"winshell {command.Name} {name} needs {match.Missing}.");
        if (match.AtMost is int most && words > most)
            throw Unexpected(positional[most + 1]);
    }

    /// <summary><c>--project</c> belongs to <c>widget install</c>. <c>--yes</c> belongs to <c>install</c> and <c>update</c>.</summary>
    private static void CheckWidget(Line line, List<string> positional)
    {
        var command = positional[0];
        if (command == "install")
            return;
        if (command == "update")
        {
            if (line.Uses.FirstOrDefault(use => use.Option == ProjectOption) is { Option: not null } project)
                throw new ArgumentException($"winshell widget update does not take \"{project.Token}\".");
            return;
        }

        if (line.Uses.FirstOrDefault(use => use.Option == ProjectOption || use.Option == YesOption) is
            { Option: not null } stray)
            throw new ArgumentException($"winshell widget {command} does not take \"{stray.Token}\".");
    }

    /// <summary>
    /// <c>inspect record</c> needs a time and an MP4, and records a window or a screen, not both. <c>scroll</c> needs a
    /// number of steps, and <c>key</c> keys it can press. An option means nothing to the commands it isn't for.
    /// </summary>
    private static void CheckInspect(Line line, CliOptions options, List<string> positional)
    {
        var name = positional[0];
        var own = name switch
        {
            "record" => Record,
            "press" => [RightOption],
            _ => []
        };
        if (line.Uses.FirstOrDefault(use => use.Option is not null && !own.Contains(use.Option) &&
                                            (Record.Contains(use.Option) || use.Option == RightOption)) is
            { Option: not null } stray)
            throw new ArgumentException($"winshell inspect {name} does not take \"{stray.Token}\".");
        if (name == "scroll" && (!int.TryParse(positional[2], NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var steps) || steps == 0 || Math.Abs(steps) > MaxSteps))
            throw new ArgumentException(
                $"winshell inspect scroll needs a number of steps from 1 to {MaxSteps}, or -1 to -{MaxSteps} to scroll up.");
        if (name == "key")
            CheckKeys(positional[1]);
        if (name != "record")
            return;

        if (options.For is null)
            throw new ArgumentException("winshell inspect record needs --for and a time, such as --for 5s.");
        if (!positional[1].EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("winshell inspect record writes an MP4, so the file's name must end in .mp4.");
        if (options.Screen is not null && positional.Count > 2)
            throw new ArgumentException("--screen records a whole screen, so it takes no window.");
        if (positional.Count > 3)
            throw Unexpected(positional[3]);
    }

    /// <summary>The most wheel notches one <c>inspect scroll</c> turns.</summary>
    private const int MaxSteps = 100;

    /// <summary>Keys <c>inspect key</c> can press: chords as a hotkey writes them, without the Windows key.</summary>
    private static void CheckKeys(string keys)
    {
        if (!HotkeySequence.TryParse(keys, out var sequence, out var error))
            throw new ArgumentException(error ?? "winshell inspect key needs keys, such as Tab or Ctrl+S.");
        if (sequence!.Chords.FirstOrDefault(chord => (chord.Modifiers & HotkeyModifiers.Win) != 0) is { } win)
            throw new ArgumentException(
                $"winshell inspect key does not press {win.Text}: Windows itself takes the Windows key, not the shell.");
    }

    private static string ConfigName(string option, string name)
    {
        if (!CommandWords.IsConfigName(name))
            throw new ArgumentException(
                $"{option} needs the name of a config folder, not \"{name}\". Use -p for a path.");
        return name;
    }

    /// <summary>
    /// A process id or a tail count. A value that isn't one is an <see cref="ArgumentException"/>,
    /// which <see cref="Cli.RunAsync"/> turns into exit code 2.
    /// </summary>
    private static int WholeNumber(string option, string text)
    {
        // NumberStyles.Integer keeps "+5", surrounding whitespace and "-0" (which is 0). A negative is not a whole number.
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0)
            throw new ArgumentException($"{option} needs a whole number.");
        return value;
    }

    /// <summary>A frame rate: a whole number above 0.</summary>
    private static int Positive(string option, string text)
    {
        var value = WholeNumber(option, text);
        if (value == 0)
            throw new ArgumentException($"{option} needs a whole number above 0.");
        return value;
    }

    /// <summary>A time: seconds, such as <c>5</c> or <c>2.5s</c>, or milliseconds, such as <c>500ms</c>. At least 1 ms.</summary>
    private static TimeSpan Duration(string option, string text)
    {
        var (number, milliseconds) = text.EndsWith("ms", StringComparison.Ordinal) ? (text[..^2], 1.0)
            : text.EndsWith('s') ? (text[..^1], 1000.0)
            : (text, 1000.0);
        if (!double.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            || value * milliseconds is < 1 or > int.MaxValue)
            throw new ArgumentException($"{option} needs a time, such as 5s or 500ms.");
        return TimeSpan.FromMilliseconds(Math.Round(value * milliseconds));
    }

    private static ArgumentException UnknownCommand(string command)
    {
        return new ArgumentException($"Unknown command \"{command}\". Run winshell --help.");
    }

    private static ArgumentException Unexpected(string argument)
    {
        return new ArgumentException($"Unexpected argument \"{argument}\".");
    }

    /// <summary>How many positional words a command takes after its name.</summary>
    private enum WordRule
    {
        None,
        One,
        Topic,
        Subcommand
    }

    /// <summary>An option's spellings, whether it takes a value, and what it sets.</summary>
    private sealed class Option(CommandWord word, bool takesValue, Func<CliOptions, Use, CliOptions> set)
    {
        public bool TakesValue => takesValue;

        /// <summary>Takes the next word as its value only when that is a whole number, as <c>--screen [n]</c> does.</summary>
        public bool TakesNumber { get; private init; }

        public static Option Flag(CommandWord word, Func<CliOptions, CliOptions> set)
        {
            return new Option(word, false, (options, _) => set(options));
        }

        public static Option Value(CommandWord word, Func<CliOptions, Use, CliOptions> set)
        {
            return new Option(word, true, set);
        }

        /// <summary>An option with an optional whole number after it. Without one, its value is null.</summary>
        public static Option Number(CommandWord word, Func<CliOptions, Use, CliOptions> set)
        {
            return new Option(word, false, set) { TakesNumber = true };
        }

        public CliOptions Set(CliOptions options, Use use)
        {
            return set(options, use);
        }

        public void Collect(Dictionary<string, Option> into)
        {
            into.TryAdd(word.Long, this);
            foreach (var name in word.Shorts)
                into.TryAdd(name, this);
        }
    }

    /// <summary>
    /// A word of <c>ipc</c>, <c>inspect</c> or <c>widget</c>. <see cref="Words"/> is how many words must follow it,
    /// and <see cref="Word"/> is one that must be first. <see cref="RawAfter"/> means the rest are that command's
    /// arguments, even when they look like options. <see cref="AtMost"/> refuses a spare word.
    /// </summary>
    private readonly record struct Subcommand(
        string Name,
        int Words = 0,
        string? Missing = null,
        string? Word = null,
        bool RawAfter = false,
        int? AtMost = null);

    private sealed record Command(string Name, string Usage, Option[] Options, Subcommand[] Subcommands, WordRule Words)
    {
        public bool Accepts(Option? option)
        {
            return option is not null && Options.Contains(option);
        }
    }

    /// <summary>An option on the line. <see cref="Option"/> is null when no option is spelled <see cref="Token"/>.</summary>
    private readonly record struct Use(string Token, Option? Option, string? Value, bool Missing);

    /// <summary>One walk over the arguments. The command is known by the end, including when it comes after its options.</summary>
    private sealed class Line(string[] args)
    {
        public string[] Args { get; } = args;

        public string? CommandName { get; private set; }

        public bool RequestsHelp { get; private set; }

        public bool Version { get; private set; }

        public List<Use> Uses { get; } = [];

        public List<string> Positional { get; } = [];

        public List<string> Rest { get; } = [];

        /// <summary>Every token except a daemonize flag. A <c>-d</c> that is some other option's value stays.</summary>
        public List<string> Child { get; } = [];

        private bool _raw;

        public void Read()
        {
            for (var i = 0; i < Args.Length; i++)
            {
                var token = Args[i];
                if (_raw)
                {
                    Positional.Add(token);
                    Child.Add(token);
                    continue;
                }

                if (token == CommandWords.End)
                {
                    if (CommandName is null or CommandWords.Run)
                    {
                        Rest.AddRange(Args.Skip(i));
                        Child.AddRange(Args.Skip(i));
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
                        if (i + 1 >= Args.Length)
                            missing = true;
                        else
                            value = Args[++i];
                    }
                    else if (option.TakesNumber && i + 1 < Args.Length &&
                             int.TryParse(Args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    {
                        value = Args[++i];
                    }

                    Uses.Add(new Use(token, option, value, missing));
                    if (option != DaemonizeOption)
                    {
                        Child.Add(token);
                        if (value is not null)
                            Child.Add(value);
                    }

                    if (option == HelpOption)
                        RequestsHelp = true;
                    else if (option == VersionOption)
                        Version = true;
                    continue;
                }

                // To inspect, a negative number is a word, the steps of scroll, and not an option.
                if (token.StartsWith('-') && !(CommandName == "inspect" && int.TryParse(token,
                        NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _)))
                {
                    Uses.Add(new Use(token, null, null, false));
                    Child.Add(token);
                    continue;
                }

                if (CommandName is null)
                {
                    if (!Commands.ContainsKey(token))
                        throw UnknownCommand(token);
                    CommandName = token;
                    Child.Add(token);
                    continue;
                }

                Positional.Add(token);
                Child.Add(token);
                if (CommandName is { } name && Commands[name].Words == WordRule.Subcommand)
                {
                    var sub = Commands[name].Subcommands.FirstOrDefault(candidate => candidate.Name == Positional[0]);
                    if (sub.RawAfter && Positional.Count >= sub.Words + 1)
                        _raw = true;
                }
            }
        }
    }
}
