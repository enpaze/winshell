using System.Runtime.InteropServices;
using Windows.Win32.Media.Audio;
using Windows.Win32.System.Com;

namespace Winshell.Native;

internal static class CoreAudio
{
    private static readonly Guid PolicyConfigClsid = new("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9");

    public static readonly PROPERTYKEY DeviceFriendlyName = new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
    public static readonly PROPERTYKEY EndpointFormFactor = new(new Guid("1DA5D803-D492-4EDD-8C23-E0C0FFEE7F0E"), 0);
    public static readonly PROPERTYKEY EngineDeviceFormat = new(new Guid("F19F064D-082C-4E27-BC73-6882A1BB8E4C"), 0);

    public static IMMDeviceEnumerator CreateEnumerator()
    {
        return Create<IMMDeviceEnumerator>(typeof(MMDeviceEnumerator).GUID);
    }

    public static void SetDefaultEndpoint(string deviceId)
    {
        var policy = Create<IPolicyConfig>(PolicyConfigClsid);
        try
        {
            Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(deviceId, ERole.eConsole));
            Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(deviceId, ERole.eMultimedia));
        }
        finally
        {
            Com.Release(policy);
        }
    }

    public static T Activate<T>(IMMDevice device)
        where T : class
    {
        var iid = typeof(T).GUID;
        device.Activate(ref iid, CLSCTX.CLSCTX_ALL, 0, out var obj);
        return (T)obj;
    }

    public static IAudioClient? StartSharedStream(IMMDevice device)
    {
        var client = Activate<IAudioClient>(device);
        if (client.GetMixFormat(out var format) == 0)
            try
            {
                if (client.Initialize(AUDCLNT_SHAREMODE.AUDCLNT_SHAREMODE_SHARED, 0, 1_000_000, 0, format, 0) == 0 &&
                    client.Start() == 0)
                    return client;
            }
            finally
            {
                Marshal.FreeCoTaskMem(format);
            }

        Com.Release(client);
        return null;
    }

    private static T Create<T>(Guid clsid)
        where T : class
    {
        var created = Com.Create<T>(clsid, CLSCTX.CLSCTX_SERVER, out var hr);
        if (created is not null)
            return created;
        Marshal.ThrowExceptionForHR(hr);
        throw new InvalidCastException();
    }

    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        void GetMixFormat();

        void GetDeviceFormat();

        void ResetDeviceFormat();

        void SetDeviceFormat();

        void GetProcessingPeriod();

        void SetProcessingPeriod();

        void GetShareMode();

        void SetShareMode();

        void GetPropertyValue();

        void SetPropertyValue();

        [PreserveSig]
        int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
    }
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    void EnumAudioEndpoints(EDataFlow flow, DEVICE_STATE stateMask, out IMMDeviceCollection devices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(EDataFlow flow, ERole role, out IMMDevice device);

    [PreserveSig]
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

    void RegisterEndpointNotificationCallback(IMMNotificationClient client);

    void UnregisterEndpointNotificationCallback(IMMNotificationClient client);
}

[ComImport]
[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    void GetCount(out uint count);

    void Item(uint index, out IMMDevice device);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    void Activate(ref Guid iid, CLSCTX clsCtx, nint activationParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object instance);

    void OpenPropertyStore(uint access, out IPropertyStore properties);

    void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

    void GetState(out uint state);
}

[ComImport]
[Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMNotificationClient
{
    void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, uint newState);

    void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

    void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

    void OnDefaultDeviceChanged(EDataFlow flow, ERole role, [MarshalAs(UnmanagedType.LPWStr)] string? defaultDeviceId);

    void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, PROPERTYKEY key);
}

[ComImport]
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    void RegisterControlChangeNotify(IAudioEndpointVolumeCallback notify);

    void UnregisterControlChangeNotify(IAudioEndpointVolumeCallback notify);

    void GetChannelCount(out uint count);

    void SetMasterVolumeLevel(float levelDb, ref Guid context);

    void SetMasterVolumeLevelScalar(float level, ref Guid context);

    void GetMasterVolumeLevel(out float levelDb);

    void GetMasterVolumeLevelScalar(out float level);

    void SetChannelVolumeLevel(uint channel, float levelDb, ref Guid context);

    void SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);

    void GetChannelVolumeLevel(uint channel, out float levelDb);

    void GetChannelVolumeLevelScalar(uint channel, out float level);

    void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);

    void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}

[ComImport]
[Guid("657804FA-D6AD-4496-8A60-352752AF4F89")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolumeCallback
{
    [PreserveSig]
    int OnNotify(nint data);
}

[ComImport]
[Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioMeterInformation
{
    [PreserveSig]
    int GetPeakValue(out float peak);

    [PreserveSig]
    int GetMeteringChannelCount(out uint count);

    [PreserveSig]
    int GetChannelsPeakValues(uint count, [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] float[] peaks);
}

[ComImport]
[Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioClient
{
    [PreserveSig]
    int Initialize(AUDCLNT_SHAREMODE shareMode, uint streamFlags, long bufferDuration, long periodicity, nint format,
        nint sessionGuid);

    [PreserveSig]
    int GetBufferSize(out uint frames);

    [PreserveSig]
    int GetStreamLatency(out long latency);

    [PreserveSig]
    int GetCurrentPadding(out uint frames);

    [PreserveSig]
    int IsFormatSupported(AUDCLNT_SHAREMODE shareMode, nint format, out nint closest);

    [PreserveSig]
    int GetMixFormat(out nint format);

    [PreserveSig]
    int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);

    [PreserveSig]
    int Start();

    [PreserveSig]
    int Stop();
}

[ComImport]
[Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionManager2
{
    void GetAudioSessionControl(nint sessionGuid, uint flags, out IAudioSessionControl2 control);

    void GetSimpleAudioVolume(nint sessionGuid, uint flags, out ISimpleAudioVolume volume);

    void GetSessionEnumerator(out IAudioSessionEnumerator sessions);

    void RegisterSessionNotification(IAudioSessionNotification notification);

    void UnregisterSessionNotification(IAudioSessionNotification notification);
}

[ComImport]
[Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionEnumerator
{
    void GetCount(out int count);

    void GetSession(int index, out IAudioSessionControl2 session);
}

[ComImport]
[Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionControl2
{
    void GetState(out AudioSessionState state);

    void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);

    void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);

    void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);

    void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);

    void GetGroupingParam(out Guid param);

    void SetGroupingParam(ref Guid param, ref Guid context);

    void RegisterAudioSessionNotification(IAudioSessionEvents events);

    void UnregisterAudioSessionNotification(IAudioSessionEvents events);

    void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);

    void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);

    void GetProcessId(out uint processId);

    [PreserveSig]
    int IsSystemSoundsSession();
}

[ComImport]
[Guid("641DD20B-4D41-49CC-ABA3-174B9477BB08")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionNotification
{
    [PreserveSig]
    int OnSessionCreated(IAudioSessionControl2 session);
}

[ComImport]
[Guid("24918ACC-64B3-37C1-8CA9-74A66E9957A8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionEvents
{
    [PreserveSig]
    int OnDisplayNameChanged([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);

    [PreserveSig]
    int OnIconPathChanged([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);

    [PreserveSig]
    int OnSimpleVolumeChanged(float volume, [MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);

    [PreserveSig]
    int OnChannelVolumeChanged(uint count, nint volumes, uint changedChannel, ref Guid context);

    [PreserveSig]
    int OnGroupingParamChanged(ref Guid param, ref Guid context);

    [PreserveSig]
    int OnStateChanged(AudioSessionState state);

    [PreserveSig]
    int OnSessionDisconnected(int reason);
}

[ComImport]
[Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISimpleAudioVolume
{
    void SetMasterVolume(float level, ref Guid context);

    void GetMasterVolume(out float level);

    void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);

    void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}
