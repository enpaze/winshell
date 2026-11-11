namespace Winshell.Session;

/// <summary>
/// Whether this configuration runs when the current user signs in. An unpackaged Winshell is a <c>Winshell</c> value
/// under the user's <c>Run</c> key; the value starts this install's <c>winshell.exe</c> running this configuration. A
/// packaged Winshell is the package's startup task. Windows' Startup apps page lists either and can turn it off.
/// Nothing is registered until the shell sets <see cref="RunAtSignIn"/>, and Winshell never changes what that page chose.
/// </summary>
public sealed class StartupService : ObservableObject
{
    private readonly Log _log;
    private readonly IStartupRegistry? _registry;
    private SignInVerdict _answer;

    internal StartupService(Log log, IStartupRegistry? registry)
    {
        _log = log;
        _registry = registry;
        Refresh();
    }

    /// <summary>
    /// The service the running shell uses: the package's startup task when packaged; for a configuration program, the
    /// <c>Run</c> value that starts it under the <c>winshell.exe</c> supervising this process; otherwise the <c>Run</c>
    /// value for this process's command. Tests construct the service with a fake instead.
    /// </summary>
    internal static StartupService ForThisProcess(Log log)
    {
        return new StartupService(log, SignInProcess.Registry(log));
    }

    /// <summary>
    /// The sign-in command for the configuration program <paramref name="program"/> supervised by
    /// <paramref name="supervisor"/>, or null when <paramref name="program"/> is not a configuration program or
    /// <paramref name="supervisor"/> is not <c>winshell.exe</c>.
    /// </summary>
    internal static string? SignInCommand(string program, string? supervisor)
    {
        if (supervisor is null || !WinshellHost.Is(supervisor) ||
            ConfigurationProject.RunPath(program) is not { } project)
            return null;
        return CommandLine.ForSignIn(supervisor, [CommandWords.Run, CommandWords.Path.Short!, project]);
    }

    /// <summary>
    /// Whether this shell can register itself to run at sign-in. False when a packaged shell has no startup task, and
    /// false when this process is a configuration program that no <c>winshell.exe</c> is supervising.
    /// </summary>
    public bool Available => Read(_registry) is not null;

    /// <summary>
    /// Whether this configuration is registered to run at sign-in. Setting it adds or removes the entry, and setting
    /// it when the entry starts another install or another configuration points the entry here: this install's
    /// <c>winshell.exe run</c> of this configuration.
    /// </summary>
    public bool RunAtSignIn
    {
        get => Read(_answer.RunAtSignIn);
        set
        {
            if (_registry is null)
            {
                _log.Warn("startup",
                    "Run at sign-in needs the winshell.exe that is supervising this configuration.");
                return;
            }

            if (value == _answer.RunAtSignIn)
                return;
            if (!(value ? _registry.Register() : _registry.Unregister()))
                _log.Warn("startup",
                    value
                        ? "Windows did not let Winshell register to run at sign-in."
                        : "Windows did not let Winshell stop running at sign-in.");
            Refresh();
        }
    }

    /// <summary>
    /// True when the entry is there but Windows' Startup apps page turned it off. Only that page can turn it back on.
    /// </summary>
    public bool TurnedOffInWindows => Read(_answer.TurnedOffInWindows);

    /// <summary>
    /// True when the entry is there but starts another install or another configuration: a <c>winshell.exe</c> in
    /// another folder, as after an update unzipped somewhere new, or a different configuration.
    /// <see cref="RunAtSignIn"/> is false until it is set.
    /// </summary>
    public bool RegisteredElsewhere => Read(_answer.RegisteredElsewhere);

    /// <summary>Reads the entry again, after the user may have changed it in Windows.</summary>
    public void Refresh()
    {
        var answer = _registry?.Read() ?? default;
        var runs = _answer.RunAtSignIn;
        var elsewhere = _answer.RegisteredElsewhere;
        var turnedOff = _answer.TurnedOffInWindows;
        _answer = answer;
        Changed(runs != answer.RunAtSignIn, nameof(RunAtSignIn));
        Changed(elsewhere != answer.RegisteredElsewhere, nameof(RegisteredElsewhere));
        Changed(turnedOff != answer.TurnedOffInWindows, nameof(TurnedOffInWindows));
    }
}
