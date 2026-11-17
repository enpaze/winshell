using Microsoft.Win32;
using Winshell.Native;

namespace Winshell.Session;

internal sealed class SessionBackend : ISessionBackend
{
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
