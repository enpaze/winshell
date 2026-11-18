namespace Winshell.Network;

public sealed class WifiDevice : NetworkDevice
{
    private readonly ReportedDeviceAction _scan;

    internal WifiDevice(NetworkDeviceInfo info, IEventLoop loop, INetworkBackend backend)
        : base(info, loop, backend)
    {
        _scan = new ReportedDeviceAction(loop.Post, () => Removed, Changed, error => OperationFailed?.Invoke(error),
            nameof(Scanning));
    }

    public event Action<string>? OperationFailed;

    public WifiDeviceMode Mode => Read(Info).Mode;

    public bool Scanning => Read(_scan).Running;

    public string Error => Read(_scan.Error);

    public void Scan()
    {
        _ = ScanAsync();
    }

    internal Task ScanAsync()
    {
        return _scan.RunAsync(() => Backend.ScanAsync(NativeId));
    }

    internal override void Update(NetworkDeviceInfo info)
    {
        var old = Info;
        base.Update(info);
        Changed(old.Mode != info.Mode, nameof(Mode));
    }
}
