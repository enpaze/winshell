namespace Winshell.Session;

/// <summary>
/// The signed-in Windows session the shell runs in. <see cref="Lock"/>, <see cref="SignOut"/>, <see cref="Restart"/>,
/// <see cref="ShutDown"/>, <see cref="Sleep"/> and <see cref="Hibernate"/> ask Windows, and Windows' own screens and
/// prompts take over. Winshell never draws a lock screen, asks for the Windows password, or confirms the action.
/// A configuration that wants a confirmation asks for it itself.
/// </summary>
public sealed class SessionService
{
    private readonly Log _log;
    private readonly ISessionBackend _backend;

    internal SessionService(Log log, ISessionBackend? backend = null)
    {
        _log = log;
        _backend = backend ?? new SessionBackend();
    }

    /// <summary>
    /// Asks Windows to lock the session, as Win+L does. Returns whether Windows took the request; the lock itself
    /// happens a moment later, in Windows.
    /// </summary>
    public bool Lock()
    {
        return Ask(_backend.Lock(), "Windows did not lock the session.");
    }

    /// <summary>
    /// Asks Windows to sign out of the session. An app with unsaved work can still hold it up, and Windows shows its
    /// own prompt. Apps come back after the next sign-in only when the user has that setting on. Returns whether
    /// Windows took the request.
    /// </summary>
    public bool SignOut()
    {
        return Ask(_backend.SignOut(), "Windows did not sign out.");
    }

    /// <summary>
    /// Asks Windows to restart the PC. Apps are not forced closed. Registered apps come back after sign-in only when
    /// the user has that setting on. Returns whether Windows took the request.
    /// </summary>
    public bool Restart()
    {
        return Ask(_backend.Restart(), "Windows did not restart.");
    }

    /// <summary>
    /// Asks Windows to shut the PC down and power it off, requesting fast startup so that setting can apply. Apps are
    /// not forced closed. Returns whether Windows took the request.
    /// </summary>
    public bool ShutDown()
    {
        return Ask(_backend.ShutDown(), "Windows did not shut down.");
    }

    /// <summary>
    /// Asks Windows to sleep, on a Modern Standby PC or one that sleeps with S3. Returns whether Windows took the
    /// request.
    /// </summary>
    public bool Sleep()
    {
        return Ask(_backend.Sleep(), "Windows did not sleep.");
    }

    /// <summary>
    /// Asks Windows to hibernate. Returns whether Windows took the request. <see cref="HibernateAvailable"/> is false
    /// when hibernate is turned off.
    /// </summary>
    public bool Hibernate()
    {
        return Ask(_backend.Hibernate(), "Windows did not hibernate.");
    }

    /// <summary>Whether this PC can sleep, including Modern Standby, so a menu can hide Sleep when it cannot.</summary>
    public bool SleepAvailable => _backend.SleepAvailable;

    /// <summary>Whether hibernate is turned on for this PC, so a menu can hide Hibernate when it cannot.</summary>
    public bool HibernateAvailable => _backend.HibernateAvailable;

    private bool Ask(bool accepted, string warning)
    {
        if (accepted)
            return true;
        _log.Warn("session", warning);
        return false;
    }
}
