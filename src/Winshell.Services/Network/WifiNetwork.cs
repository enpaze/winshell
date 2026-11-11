namespace Winshell.Network;

/// <summary>A Wi-Fi network in range.</summary>
public sealed class WifiNetwork : Network
{
    private readonly DeviceAction _action;
    private string _error = "";

    internal WifiNetwork(NetworkDevice device, NetworkInfo info)
        : base(device, info)
    {
        _action = new DeviceAction(device.Loop.Post);
    }

    /// <summary>An action failed, with the same text as <see cref="Error"/>.</summary>
    public event Action<string>? OperationFailed;

    /// <summary>The signal strength, from 0 to 1.</summary>
    public double SignalStrength => Read(Info).SignalStrength;

    /// <summary>How the network is secured.</summary>
    public WifiSecurityType Security => Read(Info).Security;

    /// <summary>An action is running. Other calls are ignored until it ends.</summary>
    public bool Busy => Read(_action).Running;

    /// <summary>Why the last action failed. Empty while an action runs and after one succeeds.</summary>
    public string Error => Read(_error);

    /// <summary>Connects to this network with saved credentials.</summary>
    public void Connect()
    {
        _ = ConnectAsync(null);
    }

    /// <summary>Connects with a password, for a secured network that isn't saved. The password is not kept.</summary>
    public void ConnectWithPassword(string password)
    {
        _ = ConnectAsync(password);
    }

    /// <summary>Connects with a saved profile of this network. The next snapshot reports whether the adapter associated.</summary>
    public void ConnectWithProfile(WifiProfile? profile)
    {
        _ = ConnectProfileAsync(profile);
    }

    /// <summary>
    /// Disconnects this network's adapter. Fails until <see cref="NetworkingService.RequestAccess"/> is allowed.
    /// </summary>
    public void Disconnect()
    {
        _ = DisconnectAsync();
    }

    /// <summary>Deletes the saved profile whose name is this SSID.</summary>
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
        // A stored reading: notify when the number changes, not when it passes a tolerance.
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
