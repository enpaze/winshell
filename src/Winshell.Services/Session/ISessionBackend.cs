namespace Winshell.Session;

/// <summary>
/// What <see cref="SessionService"/> asks Windows. Tests use a fake, so they never lock, sign out, or power this PC.
/// </summary>
internal interface ISessionBackend
{
    /// <summary>Asks Windows to lock the session. Returns whether Windows took the request.</summary>
    bool Lock();

    /// <summary>Asks Windows to sign out. Returns whether Windows took the request.</summary>
    bool SignOut();

    /// <summary>Asks Windows to restart. Returns whether Windows took the request.</summary>
    bool Restart();

    /// <summary>Asks Windows to shut down and power off. Returns whether Windows took the request.</summary>
    bool ShutDown();

    /// <summary>Asks Windows to sleep. Returns whether Windows took the request.</summary>
    bool Sleep();

    /// <summary>Asks Windows to hibernate. Returns whether Windows took the request.</summary>
    bool Hibernate();

    /// <summary>Whether this PC can sleep, including Modern Standby.</summary>
    bool SleepAvailable { get; }

    /// <summary>Whether hibernate is turned on for this PC.</summary>
    bool HibernateAvailable { get; }
}
