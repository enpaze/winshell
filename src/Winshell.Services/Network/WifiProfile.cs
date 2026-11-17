namespace Winshell.Network;

public sealed class WifiProfile : ObservableObject
{
    private readonly Network _network;
    private readonly DeviceAction _write;
    private WifiProfileInfo _info;
    private string _actionError = "";

    internal WifiProfile(Network network, WifiProfileInfo info)
    {
        _network = network;
        _info = info;
        _write = new DeviceAction(network.Device.Loop.Post);
    }

    public event Action<string>? OperationFailed;

    internal Network Network => _network;

    internal bool Hidden => _info.Hidden;

    internal bool Removed { get; private set; }

    public string Id => Read(_info).Id;

    public string Name => Read(_info).Name;

    public string Ssid => Read(_info).Ssid;

    public bool AutoConnect
    {
        get => Read(_info).AutoConnect;
        set
        {
            if (Removed || _write.Running || value == _info.AutoConnect)
                return;
            _ = SetAutoConnectAsync(value);
        }
    }

    public WifiSecurityType Security => Read(_info).Security;

    public bool HasPassword => Read(_info).HasPassword;

    public bool Loaded => Read(_info).Loaded;

    public string Error => Read(_actionError).Length > 0 ? _actionError : _info.Error;

    public void ClearPassword()
    {
        _ = ClearPasswordAsync();
    }

    public void Forget()
    {
        _ = ForgetAsync();
    }

    internal Task SetAutoConnectAsync(bool value)
    {
        return WriteAsync(() =>
            _network.Device.Backend.SetProfileAutoConnectAsync(_network.Device.NativeId, _info.Name, value));
    }

    internal Task ClearPasswordAsync()
    {
        return WriteAsync(() =>
            _network.Device.Backend.ClearProfilePasswordAsync(_network.Device.NativeId, _info.Name));
    }

    internal Task ForgetAsync()
    {
        return WriteAsync(() => _network.Device.Backend.ForgetAsync(_network.Device.NativeId, _info.Name));
    }

    internal void Update(WifiProfileInfo info)
    {
        var old = _info;
        var oldError = Error;
        _info = info;
        Changed(old.Name != info.Name, nameof(Name));
        Changed(old.Ssid != info.Ssid, nameof(Ssid));
        Changed(old.AutoConnect != info.AutoConnect, nameof(AutoConnect));
        Changed(old.Security != info.Security, nameof(Security));
        Changed(old.HasPassword != info.HasPassword, nameof(HasPassword));
        Changed(old.Loaded != info.Loaded, nameof(Loaded));
        Changed(oldError != Error, nameof(Error));
    }

    internal void Remove()
    {
        Removed = true;
    }

    private void SetActionError(string error)
    {
        var old = Error;
        _actionError = error;
        Changed(old != Error, nameof(Error));
    }

    private Task WriteAsync(Func<Task<string?>> action)
    {
        if (Removed)
            return Task.CompletedTask;
        return _write.RunAsync(() => SetActionError(""), action, error =>
        {
            if (Removed)
                return;
            SetActionError(error);
            if (error.Length > 0)
                OperationFailed?.Invoke(error);
        });
    }
}
