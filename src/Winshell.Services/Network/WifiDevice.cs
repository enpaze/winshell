namespace Winshell.Network;

public sealed class WifiDevice : NetworkDevice
{
    private readonly DeviceAction _scan;
    private string _error = "";

    internal WifiDevice(NetworkDeviceInfo info, IEventLoop loop, INetworkBackend backend)
        : base(info, loop, backend)
    {
        _scan = new DeviceAction(loop.Post);
    }

    public event Action<string>? OperationFailed;

    public WifiDeviceMode Mode => Read(Info).Mode;

    public bool Scanning => Read(_scan).Running;

    public string Error => Read(_error);

    public void Scan()
    {
        _ = ScanAsync();
    }

    internal Task ScanAsync()
    {
        if (Removed)
            return Task.CompletedTask;
        return _scan.RunAsync(() =>
        {
            Changed(nameof(Scanning));
            Set(ref _error, "", nameof(Error));
        }, async () =>
        {
            return await Backend.ScanAsync(NativeId).ConfigureAwait(false);
        }, error =>
        {
            if (Removed)
                return;
            Set(ref _error, error, nameof(Error));
            if (error.Length > 0)
                OperationFailed?.Invoke(error);
        }, () =>
        {
            if (!Removed)
                Changed(nameof(Scanning));
        });
    }

    internal override void Update(NetworkDeviceInfo info)
    {
        var old = Info;
        base.Update(info);
        Changed(old.Mode != info.Mode, nameof(Mode));
    }
}
