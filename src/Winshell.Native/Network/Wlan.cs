using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.NetworkManagement.WiFi;

namespace Winshell.Native;

internal enum WlanInterfaceState
{
    Connected = 1,
    AdHocNetworkFormed = 2,
    Disconnecting = 3,
    Disconnected = 4,
    Associating = 5,
    Discovering = 6,
    Authenticating = 7
}

internal static unsafe class Wlan
{
    private static readonly Lock HandleLock = new();
    private static bool _opened;
    private static Action? _changed;

    private static HANDLE Handle
    {
        get
        {
            lock (HandleLock)
            {
                if (_opened)
                    return field;
                _opened = true;
                try
                {
                    if (PInvoke.WlanOpenHandle(2, out _, out var handle) == 0)
                        field = handle;
                }
                catch (DllNotFoundException)
                {
                }

                return field;
            }
        }
    }

    internal static bool Listen(Action changed, Action<Exception> failed)
    {
        var handle = Handle;
        if (handle.IsNull)
            return false;
        _changed = () => Callback.Run(failed, changed);
        try
        {
            _ = PInvoke.WlanRegisterNotification(handle, WLAN_NOTIFICATION_SOURCES.WLAN_NOTIFICATION_SOURCE_ACM, true,
                &OnNotification, null);
            return true;
        }
        catch (EntryPointNotFoundException)
        {
            _changed = null;
            return false;
        }
    }

    internal static void StopListening()
    {
        _changed = null;
        var handle = Handle;
        if (!handle.IsNull)
            _ = PInvoke.WlanRegisterNotification(handle, WLAN_NOTIFICATION_SOURCES.WLAN_NOTIFICATION_SOURCE_NONE, true,
                null, null);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnNotification(L2_NOTIFICATION_DATA* data, void* context)
    {
        _changed?.Invoke();
    }

    internal static Dictionary<Guid, WlanInterfaceState> InterfaceStates()
    {
        var result = new Dictionary<Guid, WlanInterfaceState>();
        var handle = Handle;
        if (handle.IsNull || PInvoke.WlanEnumInterfaces(handle, out var list) != 0)
            return result;
        try
        {
            foreach (ref readonly var item in list->InterfaceInfo.AsSpan((int)list->dwNumberOfItems))
                result[item.InterfaceGuid] = (WlanInterfaceState)item.isState;
        }
        finally
        {
            PInvoke.WlanFreeMemory(list);
        }

        return result;
    }

    internal static HashSet<string> ProfileNames(Guid interfaceGuid)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var handle = Handle;
        if (handle.IsNull || PInvoke.WlanGetProfileList(handle, interfaceGuid, out var list) != 0)
            return result;
        try
        {
            foreach (ref readonly var item in list->ProfileInfo.AsSpan((int)list->dwNumberOfItems))
                result.Add(item.strProfileName.ToString());
        }
        finally
        {
            PInvoke.WlanFreeMemory(list);
        }

        return result;
    }

    internal static uint? OperationMode(Guid interfaceGuid)
    {
        var handle = Handle;
        if (handle.IsNull)
            return null;
        void* data = null;
        try
        {
            if (PInvoke.WlanQueryInterface(handle, interfaceGuid,
                    WLAN_INTF_OPCODE.wlan_intf_opcode_current_operation_mode, out var size, out data) != 0
                || data == null || size < sizeof(uint))
                return null;
            return *(uint*)data;
        }
        finally
        {
            if (data != null)
                PInvoke.WlanFreeMemory(data);
        }
    }

    internal static uint DeleteProfile(Guid interfaceGuid, string profileName)
    {
        var handle = Handle;
        if (handle.IsNull)
            return (uint)WIN32_ERROR.ERROR_SERVICE_NOT_ACTIVE;
        return PInvoke.WlanDeleteProfile(handle, interfaceGuid, profileName);
    }

    internal static uint ProfileReadFlags()
    {
        return 0;
    }

    private const uint ProfileFlagMask = PInvoke.WLAN_PROFILE_USER | PInvoke.WLAN_PROFILE_GROUP_POLICY;

    internal static uint ProfileWriteFlags(uint flags)
    {
        return flags & ProfileFlagMask;
    }

    internal static uint GetProfile(Guid interfaceGuid, string profileName, out string? xml, out uint flags)
    {
        xml = null;
        flags = 0;
        var handle = Handle;
        if (handle.IsNull)
            return (uint)WIN32_ERROR.ERROR_SERVICE_NOT_ACTIVE;
        var requested = ProfileReadFlags();
        var error = PInvoke.WlanGetProfile(handle, interfaceGuid, profileName, out var text, ref requested, out _);
        if (text.Value == null)
            return error == 0 ? (uint)WIN32_ERROR.ERROR_NOT_FOUND : error;
        try
        {
            if (error != 0)
                return error;
            xml = text.ToString();
            flags = ProfileWriteFlags(requested);
            return 0;
        }
        finally
        {
            PInvoke.WlanFreeMemory(text.Value);
        }
    }

    internal static uint SetProfile(Guid interfaceGuid, string profileXml, uint flags, out uint reason)
    {
        reason = 0;
        var handle = Handle;
        if (handle.IsNull)
            return (uint)WIN32_ERROR.ERROR_SERVICE_NOT_ACTIVE;
        if (string.IsNullOrEmpty(profileXml))
            return (uint)WIN32_ERROR.ERROR_INVALID_PARAMETER;
        return PInvoke.WlanSetProfile(handle, interfaceGuid, ProfileWriteFlags(flags), profileXml, null, true,
            out reason);
    }

    internal static uint ConnectProfile(Guid interfaceGuid, string profileName, bool hidden)
    {
        var handle = Handle;
        if (handle.IsNull)
            return (uint)WIN32_ERROR.ERROR_SERVICE_NOT_ACTIVE;
        if (string.IsNullOrEmpty(profileName))
            return (uint)WIN32_ERROR.ERROR_INVALID_PARAMETER;
        fixed (char* name = profileName)
        {
            var parameters = new WLAN_CONNECTION_PARAMETERS
            {
                wlanConnectionMode = WLAN_CONNECTION_MODE.wlan_connection_mode_profile,
                strProfile = name,
                dot11BssType = DOT11_BSS_TYPE.dot11_BSS_type_any,
                dwFlags = hidden ? PInvoke.WLAN_CONNECTION_HIDDEN_NETWORK : 0
            };
            return PInvoke.WlanConnect(handle, interfaceGuid, parameters);
        }
    }
}
