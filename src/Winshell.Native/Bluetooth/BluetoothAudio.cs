using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.KernelStreaming;
using Windows.Win32.Storage.FileSystem;

namespace Winshell.Native;

/// <summary>The one-shot connection requests of a Bluetooth audio driver.</summary>
internal static class BluetoothAudio
{
    /// <summary>KSPROPSETID_BtAudio. CsWin32 emits it as a class to create, not as a GUID value.</summary>
    private static readonly Guid PropertySet = new("7fa06c40-b8f6-4c7e-8556-e8c33a12e54d");

    /// <summary>Asks the audio filter at <paramref name="filterPath"/> to connect or disconnect. Whether the driver accepted the request.</summary>
    internal static unsafe bool SetConnection(string filterPath, bool connect)
    {
        using var device = new SafeFileHandle(PInvoke.CreateFile(filterPath,
            (uint)(GENERIC_ACCESS_RIGHTS.GENERIC_READ | GENERIC_ACCESS_RIGHTS.GENERIC_WRITE),
            FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE, null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING, default, default), ownsHandle: true);
        if (device.IsInvalid)
            return false;
        var request = new KSIDENTIFIER
        {
            Set = PropertySet,
            Id = (uint)(connect
                ? KSPROPERTY_BTAUDIO.KSPROPERTY_ONESHOT_RECONNECT
                : KSPROPERTY_BTAUDIO.KSPROPERTY_ONESHOT_DISCONNECT),
            Flags = PInvoke.KSPROPERTY_TYPE_GET
        };
        uint returned;
        return PInvoke.DeviceIoControl(new HANDLE(device.DangerousGetHandle()), PInvoke.IOCTL_KS_PROPERTY,
            &request, (uint)sizeof(KSIDENTIFIER), null, 0, &returned, null);
    }
}
