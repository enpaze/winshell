namespace Winshell.Network;

public sealed class WifiNetwork : Network
{
    private readonly ReportedDeviceAction _action;

    internal WifiNetwork(NetworkDevice device, NetworkInfo info)
        : base(device, info)
    {
        _action = new ReportedDeviceAction(device.Loop.Post, () => Removed, Changed,
            error => OperationFailed?.Invoke(error), nameof(Busy));
    }

    public event Action<string>? OperationFailed;

    public double SignalStrength => Read(Info).SignalStrength;

    public WifiSecurityType Security => Read(Info).Security;

    public bool Busy => Read(_action).Running;

    public string Error => Read(_action.Error);

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
        return _action.RunAsync(() => Device.Backend.DisconnectAsync(Device.NativeId));
    }

    internal Task ForgetAsync()
    {
        return _action.RunAsync(() => Device.Backend.ForgetAsync(Device.NativeId, Name));
    }

    internal Task ConnectAsync(string? password)
    {
        return _action.RunAsync(async () =>
        {
            var failure = await Device.Backend.ConnectAsync(Device.NativeId, Name, password).ConfigureAwait(false);
            return failure?.ToString();
        });
    }

    internal Task ConnectProfileAsync(WifiProfile? profile)
    {
        return _action.RunAsync(async () =>
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
}
