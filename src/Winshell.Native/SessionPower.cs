using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Power;
using Windows.Win32.System.Shutdown;

namespace Winshell.Native;

internal static class SessionPower
{
    private const SHUTDOWN_REASON Planned = SHUTDOWN_REASON.SHTDN_REASON_FLAG_PLANNED |
                                            SHUTDOWN_REASON.SHTDN_REASON_MAJOR_OTHER |
                                            SHUTDOWN_REASON.SHTDN_REASON_MINOR_OTHER;

    internal static bool SignOut(bool restartApps)
    {
        var flags = EXIT_WINDOWS_FLAGS.EWX_LOGOFF;
        if (restartApps)
            flags |= EXIT_WINDOWS_FLAGS.EWX_RESTARTAPPS;
        return PInvoke.ExitWindowsEx(flags, Planned);
    }

    internal static bool Restart(bool restartApps)
    {
        if (!EnableShutdownPrivilege())
            return false;
        var flags = SHUTDOWN_FLAGS.SHUTDOWN_RESTART;
        if (restartApps)
            flags |= SHUTDOWN_FLAGS.SHUTDOWN_RESTARTAPPS;
        return Shutdown(flags);
    }

    internal static bool ShutDown()
    {
        if (!EnableShutdownPrivilege())
            return false;
        return Shutdown(SHUTDOWN_FLAGS.SHUTDOWN_POWEROFF | SHUTDOWN_FLAGS.SHUTDOWN_HYBRID);
    }

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

    private static unsafe bool TurnScreenOff()
    {
        return PInvoke.CallNtPowerInformation(POWER_INFORMATION_LEVEL.ScreenOff, null, 0, null, 0).Value >= 0;
    }

    private static bool Suspend(bool hibernate)
    {
        if (!EnableShutdownPrivilege())
            return false;
        return PInvoke.SetSuspendState(hibernate, false, false);
    }

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
