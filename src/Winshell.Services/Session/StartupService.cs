namespace Winshell.Session;

public sealed class StartupService : ObservableObject
{
    private readonly Log _log;
    private readonly IStartupBackend? _backend;
    private SignInVerdict _answer;

    internal StartupService(Log log, IStartupBackend? backend)
    {
        _log = log;
        _backend = backend;
        Refresh();
    }

    internal static StartupService ForThisProcess(Log log)
    {
        return new StartupService(log, SignInProcess.Registry(log));
    }

    public bool Available => Read(_backend) is not null;

    public bool RunAtSignIn
    {
        get => Read(_answer.RunAtSignIn);
        set
        {
            if (_backend is null)
            {
                _log.Warn("startup",
                    "Run at sign-in needs the winshell.exe that is supervising this configuration.");
                return;
            }

            if (value == _answer.RunAtSignIn)
                return;
            if (!(value ? _backend.Register() : _backend.Unregister()))
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
        var answer = _backend?.Read() ?? default;
        var runs = _answer.RunAtSignIn;
        var elsewhere = _answer.RegisteredElsewhere;
        var turnedOff = _answer.TurnedOffInWindows;
        _answer = answer;
        Changed(runs != answer.RunAtSignIn, nameof(RunAtSignIn));
        Changed(elsewhere != answer.RegisteredElsewhere, nameof(RegisteredElsewhere));
        Changed(turnedOff != answer.TurnedOffInWindows, nameof(TurnedOffInWindows));
    }
}
