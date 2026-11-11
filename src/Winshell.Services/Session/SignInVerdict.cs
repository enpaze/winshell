using Windows.ApplicationModel;

namespace Winshell.Session;

/// <summary>
/// What the sign-in entry is for this install and this configuration. Doctor prints its sentences from
/// <see cref="Kind"/>. <see cref="StartupService"/> stores the value and reads <see cref="RunAtSignIn"/>,
/// <see cref="RegisteredElsewhere"/> and <see cref="TurnedOffInWindows"/> from it.
/// </summary>
internal readonly record struct SignInVerdict
{
    /// <summary>Which answer this is. Doctor's sentences switch on it.</summary>
    public SignInKind Kind { get; init; }

    /// <summary>The program the entry starts, when the command names one.</summary>
    public string? Program { get; init; }

    /// <summary>The command line. Doctor uses it when <see cref="Program"/> is missing.</summary>
    public string? Command { get; init; }

    /// <summary>Whether the command names a configuration with <c>-p</c> or <c>-c</c>.</summary>
    public bool NamesConfiguration { get; init; }

    /// <summary>Whether an entry is present. A missing program still counts: the value is there.</summary>
    public bool HasEntry { get; init; }

    /// <summary>Whether the entry starts something other than this command.</summary>
    public bool PointsElsewhere { get; init; }

    /// <summary>Whether Windows turned the entry off. True even when the entry points elsewhere.</summary>
    public bool OffInWindows { get; init; }

    public bool RunAtSignIn => HasEntry && !PointsElsewhere;

    public bool RegisteredElsewhere => HasEntry && PointsElsewhere;

    public bool TurnedOffInWindows => HasEntry && OffInWindows;

    /// <summary>The startup task's answer. <see cref="StartupTaskState.DisabledByUser"/> is registered and turned off. Nothing points elsewhere.</summary>
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

    /// <summary>
    /// The Run key's answer for <paramref name="command"/>, compared with <paramref name="thisExe"/> and
    /// <paramref name="projectFile"/>. A command that is not there is no entry. A program that is not there, or is not a
    /// path, is still an entry.
    /// </summary>
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

/// <summary>The sign-in answers. <see cref="SignInKind.Registered"/> is this configuration, on.</summary>
internal enum SignInKind
{
    /// <summary>No Run value.</summary>
    None,

    /// <summary>The package has no startup task.</summary>
    NoStartupTask,

    /// <summary>The startup task is disabled, so nothing is registered.</summary>
    NotRegistered,

    /// <summary>This configuration is registered and Windows left it on.</summary>
    Registered,

    /// <summary>Registered, and Startup apps turned it off.</summary>
    TurnedOffInWindows,

    /// <summary>Registered, and Windows policy turned it off.</summary>
    TurnedOffByPolicy,

    /// <summary>The entry names a program that is not there.</summary>
    MissingProgram,

    /// <summary>The entry starts another install.</summary>
    AnotherInstall,

    /// <summary>The entry starts another configuration.</summary>
    AnotherConfiguration
}
