namespace Winshell.Session;

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

    internal static StartupService ForThisProcess(Log log)
    {
        return new StartupService(log, SignInProcess.Registry(log));
    }

    internal static string? SignInCommand(string program, string? supervisor)
    {
        if (supervisor is null || !WinshellHost.Is(supervisor) ||
            ConfigurationProject.RunPath(program) is not { } project)
            return null;
        return CommandLine.ForSignIn(supervisor, [CommandWords.Run, CommandWords.Path.Short!, project]);
    }

    public bool Available => Read(_registry) is not null;

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

    public bool TurnedOffInWindows => Read(_answer.TurnedOffInWindows);

    public bool RegisteredElsewhere => Read(_answer.RegisteredElsewhere);

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
