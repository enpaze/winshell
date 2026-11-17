using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Power;

namespace Winshell.Native;

internal static unsafe class PowerMode
{
    private const uint ProcedureNotFound = 127;

    internal static readonly Guid BestEfficiency = new("961cc777-2547-4f9d-8174-7d86181b8a7a");

    internal static readonly Guid BestPerformance = new("ded574b5-45a0-4f42-8737-46345c09c238");

    internal static readonly Guid DeprecatedPerformance = new("3af9b8d9-7c97-431d-ad78-34a8bfea439f");

    private static Action<int>? _effectiveChanged;
    private static void* _effectiveRegistration;

    internal static uint GetUserConfigured(bool battery, out Guid mode)
    {
        try
        {
            return battery
                ? PInvoke.PowerGetUserConfiguredDCPowerMode(out mode)
                : PInvoke.PowerGetUserConfiguredACPowerMode(out mode);
        }
        catch (EntryPointNotFoundException)
        {
            mode = Guid.Empty;
            return ProcedureNotFound;
        }
    }

    internal static uint SetUserConfigured(bool battery, Guid mode)
    {
        try
        {
            return battery
                ? PInvoke.PowerSetUserConfiguredDCPowerMode(in mode)
                : PInvoke.PowerSetUserConfiguredACPowerMode(in mode);
        }
        catch (EntryPointNotFoundException)
        {
            return ProcedureNotFound;
        }
    }

    internal static bool ListenEffective(Action<int> changed, Action<Exception> failed)
    {
        StopListeningEffective();
        _effectiveChanged = mode => Callback.Run(failed, () => changed(mode));
        try
        {
            _ = PInvoke.PowerRegisterForEffectivePowerModeNotifications(PInvoke.EFFECTIVE_POWER_MODE_V2,
                &OnEffectiveChanged, null, out _effectiveRegistration);
            return true;
        }
        catch (EntryPointNotFoundException)
        {
            _effectiveChanged = null;
            _effectiveRegistration = null;
            return false;
        }
    }

    internal static void StopListeningEffective()
    {
        _effectiveChanged = null;
        if (_effectiveRegistration is null)
            return;
        _ = PInvoke.PowerUnregisterFromEffectivePowerModeNotifications(_effectiveRegistration);
        _effectiveRegistration = null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnEffectiveChanged(EFFECTIVE_POWER_MODE mode, void* context)
    {
        _effectiveChanged?.Invoke((int)mode);
    }
}
