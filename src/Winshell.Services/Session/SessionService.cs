namespace Winshell.Session;

public sealed class SessionService
{
    private readonly Log _log;
    private readonly ISessionBackend _backend;

    internal SessionService(Log log, ISessionBackend? backend = null)
    {
        _log = log;
        _backend = backend ?? new SessionBackend();
    }

    public bool Lock()
    {
        return Ask(_backend.Lock(), "Windows did not lock the session.");
    }

    public bool SignOut()
    {
        return Ask(_backend.SignOut(), "Windows did not sign out.");
    }

    public bool Restart()
    {
        return Ask(_backend.Restart(), "Windows did not restart.");
    }

    public bool ShutDown()
    {
        return Ask(_backend.ShutDown(), "Windows did not shut down.");
    }

    public bool Sleep()
    {
        return Ask(_backend.Sleep(), "Windows did not sleep.");
    }

    public bool Hibernate()
    {
        return Ask(_backend.Hibernate(), "Windows did not hibernate.");
    }

    public bool SleepAvailable => _backend.SleepAvailable;

    public bool HibernateAvailable => _backend.HibernateAvailable;

    private bool Ask(bool accepted, string warning)
    {
        if (accepted)
            return true;
        _log.Warn("session", warning);
        return false;
    }
}
