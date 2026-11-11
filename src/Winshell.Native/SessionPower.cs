using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Power;
using Windows.Win32.System.Shutdown;

namespace Winshell.Native;

/// <summary>
/// Sign out, restart, shut down, sleep and hibernate. Each call asks Windows and returns; Windows finishes the
/// action, and an app that is not forced closed can still hold a sign-out, restart or shut down up.
/// </summary>
internal static class SessionPower
{
    /// <summary>
    /// A planned shutdown for no more specific reason. Zero would be logged as an unplanned shutdown.
    /// </summary>
    private const SHUTDOWN_REASON Planned = SHUTDOWN_REASON.SHTDN_REASON_FLAG_PLANNED |
                                            SHUTDOWN_REASON.SHTDN_REASON_MAJOR_OTHER |
                                            SHUTDOWN_REASON.SHTDN_REASON_MINOR_OTHER;

    /// <summary>Signs out. Does not enable the shutdown privilege, which sign-out does not need.</summary>
    internal static bool SignOut(bool restartApps)
    {
        var flags = EXIT_WINDOWS_FLAGS.EWX_LOGOFF;
        if (restartApps)
            flags |= EXIT_WINDOWS_FLAGS.EWX_RESTARTAPPS;
        return PInvoke.ExitWindowsEx(flags, Planned);
    }

    /// <summary>Restarts. <paramref name="restartApps"/> is the user's restart-apps-after-sign-in setting.</summary>
    internal static bool Restart(bool restartApps)
    {
        if (!EnableShutdownPrivilege())
            return false;
        var flags = SHUTDOWN_FLAGS.SHUTDOWN_RESTART;
        if (restartApps)
            flags |= SHUTDOWN_FLAGS.SHUTDOWN_RESTARTAPPS;
        return Shutdown(flags);
    }

    /// <summary>
    /// Shuts down and powers off. <see cref="SHUTDOWN_FLAGS.SHUTDOWN_HYBRID"/> requests fast startup. Without it,
    /// <c>InitiateShutdown</c> always does a full shutdown, so the user's fast startup setting would not apply.
    /// </summary>
    internal static bool ShutDown()
    {
        if (!EnableShutdownPrivilege())
            return false;
        return Shutdown(SHUTDOWN_FLAGS.SHUTDOWN_POWEROFF | SHUTDOWN_FLAGS.SHUTDOWN_HYBRID);
    }

    /// <summary>
    /// Sleeps. A Modern Standby PC sleeps by turning the screen off. An S3 PC suspends. <c>SetSuspendState</c> alone
    /// does not enter Modern Standby.
    /// </summary>
    internal static bool Sleep()
    {
        var power = Read();
        if (!power.Known)
            return false;
        return power.ModernStandby ? TurnScreenOff() : Suspend(false);
    }

    internal static bool Hibernate()
    {
        return Suspend(true);
    }

    /// <summary>
    /// Whether sleep and hibernate would work. Sleep includes S1, S2, S3 and Modern Standby (<c>AoAc</c>). Hibernate
    /// requires S4 and a hibernation file, so it is false when hibernate is turned off.
    /// </summary>
    internal static void ReadAvailability(out bool sleep, out bool hibernate)
    {
        var power = Read();
        sleep = power.CanSleep;
        hibernate = power.CanHibernate;
    }

    private readonly struct PowerCaps
    {
        internal bool Known { get; init; }

        internal bool ModernStandby { get; init; }

        internal bool CanSleep { get; init; }

        internal bool CanHibernate { get; init; }
    }

    private static unsafe PowerCaps Read()
    {
        SYSTEM_POWER_CAPABILITIES caps;
        if (!PInvoke.GetPwrCapabilities(&caps))
            return default;
        var modern = (bool)caps.AoAc;
        return new PowerCaps
        {
            Known = true,
            ModernStandby = modern,
            CanSleep = caps.SystemS1 || caps.SystemS2 || caps.SystemS3 || modern,
            CanHibernate = caps.SystemS4 && caps.HiberFilePresent
        };
    }

    /// <summary>
    /// Modern Standby starts when the screen turns off. <c>SetSuspendState</c> requests classic suspend and does not
    /// enter it.
    /// </summary>
    private static unsafe bool TurnScreenOff()
    {
        return PInvoke.CallNtPowerInformation(POWER_INFORMATION_LEVEL.ScreenOff, null, 0, null, 0).Value >= 0;
    }

    private static bool Suspend(bool hibernate)
    {
        // SetSuspendState requires the shutdown privilege. Force has no effect; wake events stay enabled.
        if (!EnableShutdownPrivilege())
            return false;
        return PInvoke.SetSuspendState(hibernate, false, false);
    }

    /// <summary>
    /// Accepts the shutdown and returns. Grace is zero, and apps are not forced closed, so Windows can still show
    /// its own prompt and an app can still stop it. Another signed-in user makes Windows refuse, rather than be
    /// signed out by force.
    /// </summary>
    private static bool Shutdown(SHUTDOWN_FLAGS flags)
    {
        return PInvoke.InitiateShutdown(null, null, 0, flags, Planned) == (uint)WIN32_ERROR.ERROR_SUCCESS;
    }

    private static unsafe bool EnableShutdownPrivilege()
    {
        if (!PInvoke.OpenProcessToken(PInvoke.GetCurrentProcess(),
                TOKEN_ACCESS_MASK.TOKEN_ADJUST_PRIVILEGES | TOKEN_ACCESS_MASK.TOKEN_QUERY, out var token))
            return false;
        try
        {
            LUID luid;
            fixed (char* name = "SeShutdownPrivilege")
            {
                if (!PInvoke.LookupPrivilegeValue(null, name, &luid))
                    return false;
            }

            var privileges = new TOKEN_PRIVILEGES { PrivilegeCount = 1 };
            privileges.Privileges[0] = new LUID_AND_ATTRIBUTES
            {
                Luid = luid,
                Attributes = TOKEN_PRIVILEGES_ATTRIBUTES.SE_PRIVILEGE_ENABLED
            };
            // A stale last-error would look like failure: the call returns success and sets the error only when a
            // privilege was not assigned.
            Marshal.SetLastPInvokeError(0);
            if (!PInvoke.AdjustTokenPrivileges(token, false, &privileges, 0, null, null))
                return false;
            return Marshal.GetLastPInvokeError() == (int)WIN32_ERROR.ERROR_SUCCESS;
        }
        finally
        {
            PInvoke.CloseHandle(token);
        }
    }
}
