using Microsoft.Win32;
using Winshell.Native;

namespace Winshell.Session;

internal sealed class SessionBackend : ISessionBackend
{
    /// <summary>
    /// "Automatically save my restartable apps and restart them when I sign back in", under Sign-in options.
    /// Off is the default, including when <c>RestartApps</c> has never been written. Shutdown UX reads the same value
    /// before it asks Windows; the shutdown call itself does not.
    /// </summary>
    private const string RestartAppsKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";

    public bool Lock()
    {
        return PInvoke.LockWorkStation();
    }

    public bool SignOut()
    {
        return SessionPower.SignOut(RestartApps());
    }

    public bool Restart()
    {
        return SessionPower.Restart(RestartApps());
    }

    public bool ShutDown()
    {
        return SessionPower.ShutDown();
    }

    public bool Sleep()
    {
        return SessionPower.Sleep();
    }

    public bool Hibernate()
    {
        return SessionPower.Hibernate();
    }

    public bool SleepAvailable
    {
        get
        {
            SessionPower.ReadAvailability(out var sleep, out _);
            return sleep;
        }
    }

    public bool HibernateAvailable
    {
        get
        {
            SessionPower.ReadAvailability(out _, out var hibernate);
            return hibernate;
        }
    }

    /// <summary>True only when the user has turned restart-apps on. A missing value, or a read Windows refuses, is off.</summary>
    private static bool RestartApps()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RestartAppsKey);
            return key?.GetValue("RestartApps") is int value && value != 0;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return false;
        }
    }
}
