namespace Winshell.Network;

public sealed class WifiNetwork : Network
{
    private readonly DeviceAction _action;
    private string _error = "";

    internal WifiNetwork(NetworkDevice device, NetworkInfo info)
        : base(device, info)
    {
        _action = new DeviceAction(device.Loop.Post);
    }

    public event Action<string>? OperationFailed;

    public double SignalStrength => Read(Info).SignalStrength;

    public WifiSecurityType Security => Read(Info).Security;

    public bool Busy => Read(_action).Running;

    public string Error => Read(_error);

    public void Connect()
    {
        _ = ConnectAsync(null);
    }

    public void ConnectWithPassword(string password)
    {
        _ = ConnectAsync(password);
    }

    public void ConnectWithProfile(WifiProfile? profile)
    {
        _ = ConnectProfileAsync(profile);
    }

    public void Disconnect()
    {
        _ = DisconnectAsync();
    }

    public void Forget()
    {
        _ = ForgetAsync();
    }

    internal Task DisconnectAsync()
    {
        return RunAsync(() => Device.Backend.DisconnectAsync(Device.NativeId));
    }

    internal Task ForgetAsync()
    {
        return RunAsync(() => Device.Backend.ForgetAsync(Device.NativeId, Name));
    }

    internal Task ConnectAsync(string? password)
    {
        return RunAsync(async () =>
        {
            var failure = await Device.Backend.ConnectAsync(Device.NativeId, Name, password).ConfigureAwait(false);
            return failure?.ToString();
        });
    }

    internal Task ConnectProfileAsync(WifiProfile? profile)
    {
        return RunAsync(async () =>
        {
            if (profile is null || profile.Removed || !ReferenceEquals(profile.Network, this))
                return "That saved profile is not on this network.";
            return await Device.Backend.ConnectProfileAsync(Device.NativeId, profile.Name, profile.Hidden)
                .ConfigureAwait(false);
        });
    }

    internal override void Update(NetworkInfo info)
    {
        var old = Info;
        base.Update(info);
        Changed(old.SignalStrength != info.SignalStrength, nameof(SignalStrength));
        Changed(old.Security != info.Security, nameof(Security));
    }

    private Task RunAsync(Func<Task<string?>> action)
    {
        if (Removed)
            return Task.CompletedTask;
        return _action.RunAsync(() =>
        {
            Changed(nameof(Busy));
            Set(ref _error, "", nameof(Error));
        }, action, error =>
        {
            if (Removed)
                return;
            Set(ref _error, error, nameof(Error));
            if (error.Length > 0)
                OperationFailed?.Invoke(error);
        }, () =>
        {
            if (!Removed)
                Changed(nameof(Busy));
        });
    }
}
