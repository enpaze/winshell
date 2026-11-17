using Windows.ApplicationModel;

namespace Winshell.Session;

internal readonly record struct SignInVerdict
{
    public SignInKind Kind { get; init; }

    public string? Program { get; init; }

    public string? Command { get; init; }

    public bool NamesConfiguration { get; init; }

    public bool HasEntry { get; init; }

    public bool PointsElsewhere { get; init; }

    public bool OffInWindows { get; init; }

    public bool RunAtSignIn => HasEntry && !PointsElsewhere;

    public bool RegisteredElsewhere => HasEntry && PointsElsewhere;

    public bool TurnedOffInWindows => HasEntry && OffInWindows;

    public static SignInVerdict FromTask(StartupTaskState? state)
    {
        if (state is null)
            return new SignInVerdict { Kind = SignInKind.NoStartupTask };
        return state switch
        {
            StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy =>
                new SignInVerdict { Kind = SignInKind.Registered, HasEntry = true },
            StartupTaskState.DisabledByUser =>
                new SignInVerdict { Kind = SignInKind.TurnedOffInWindows, HasEntry = true, OffInWindows = true },
            StartupTaskState.DisabledByPolicy =>
                new SignInVerdict { Kind = SignInKind.TurnedOffByPolicy, HasEntry = true, OffInWindows = true },
            _ => new SignInVerdict { Kind = SignInKind.NotRegistered }
        };
    }

    public static SignInVerdict FromRun(string? command, bool turnedOff, string thisExe, string configRoot,
        Func<string, bool> exists, Func<string, string> resolve, string? projectFile, Log? log = null)
    {
        if (command is null)
            return new SignInVerdict { Kind = SignInKind.None };
        var launch = CommandLine.ReadSignIn(command);
        var program = launch.Program;

        SignInVerdict Answer(SignInKind kind, bool elsewhere)
        {
            return new SignInVerdict
            {
                Kind = kind,
                Program = program,
                Command = command,
                NamesConfiguration = launch.NamesConfiguration,
                HasEntry = true,
                PointsElsewhere = elsewhere,
                OffInWindows = turnedOff
            };
        }

        if (program is null || !exists(program))
            return Answer(SignInKind.MissingProgram, false);
        if (!SignInEntry.SameInstall(program, thisExe, resolve, log))
            return Answer(SignInKind.AnotherInstall, true);
        if (projectFile is not null && !SignInEntry.Starts(launch, projectFile, configRoot, log))
            return Answer(SignInKind.AnotherConfiguration, true);
        if (turnedOff)
            return Answer(SignInKind.TurnedOffInWindows, false);
        return Answer(SignInKind.Registered, false);
    }
}

internal enum SignInKind
{
    None,

    NoStartupTask,

    NotRegistered,

    Registered,

    TurnedOffInWindows,

    TurnedOffByPolicy,

    MissingProgram,

    AnotherInstall,

    AnotherConfiguration
}
