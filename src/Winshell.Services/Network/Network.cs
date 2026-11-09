namespace Winshell.Network;

/// <summary>A network of an adapter. Wi-Fi networks are <see cref="WifiNetwork"/> objects.</summary>
public class Network : ObservableObject
{
    private const string WiredConnects = "Windows manages wired connections automatically.";

    private readonly DeviceAction _action;
    private NetworkInfo _info;
    private string _error = "";

    internal Network(NetworkDevice device, NetworkInfo info)
    {
        Device = device;
        _info = info;
        _action = new DeviceAction(device.Loop.Post);
    }

    /// <summary>An action failed, with the same text as <see cref="Error"/>.</summary>
    public event Action<string>? OperationFailed;

    internal NetworkInfo Info => _info;

    private bool Removed { get; set; }

    /// <summary>The SSID of a Wi-Fi network. For a wired network, the connection profile's name, or the adapter's.</summary>
    public string Name => Read(_info).Name;

    /// <summary>The adapter this network is on.</summary>
    public NetworkDevice Device { get => Read(field); }

    /// <summary>Whether the adapter is connected to this network.</summary>
    public bool Connected => Read(_info).State == ConnectionState.Connected;

    /// <summary>A saved profile matches this network. True for a wired network.</summary>
    public bool Saved => Read(_info).Known;

    /// <summary>
    /// The adapter's connection state on this network. <see cref="ConnectionState.Disconnected"/> for every Wi-Fi
    /// network the adapter is not on.
    /// </summary>
    public ConnectionState State => Read(_info).State;

    /// <summary>An action is running. Other calls are ignored until it ends.</summary>
    public bool Busy => Read(_action).Running;

    /// <summary>Why the last action failed. Empty while an action runs and after one succeeds.</summary>
    public string Error => Read(_error);

    /// <summary>
    /// The saved Wi-Fi profiles for this network: those whose SSID is its name, and those that can't be read whose
    /// profile name is. Empty for a wired network.
    /// </summary>
    public ObservableList<WifiProfile> SavedProfiles { get => Read(field); } = [];

    /// <summary>Connects to this Wi-Fi network with saved credentials. Fails for a wired network.</summary>
    public void Connect()
    {
        _ = ConnectAsync(null);
    }

    /// <summary>Connects with a saved profile of this network. The next snapshot reports whether the adapter associated.</summary>
    public void ConnectWithProfile(WifiProfile? profile)
    {
        _ = ConnectProfileAsync(profile);
    }

    /// <summary>
    /// Disconnects this network's Wi-Fi adapter. Fails for a wired network, and until
    /// <see cref="NetworkingService.RequestAccess"/> is allowed.
    /// </summary>
    public void Disconnect()
    {
        _ = DisconnectAsync();
    }

    /// <summary>Deletes the saved Wi-Fi profile whose name is this SSID.</summary>
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
        return RunAsync(() => Device.Type == DeviceType.Wifi
            ? Device.Backend.ForgetAsync(Device.NativeId, Name)
            : Task.FromResult<string?>("Only saved Wi-Fi profiles can be forgotten."));
    }

    internal Task ConnectAsync(string? password)
    {
        return RunAsync(async () =>
        {
            if (Device.Type != DeviceType.Wifi)
                return WiredConnects;
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
            if (Device.Type != DeviceType.Wifi)
                return WiredConnects;
            return await Device.Backend.ConnectProfileAsync(Device.NativeId, profile.Name, profile.Hidden)
                .ConfigureAwait(false);
        });
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
            Changed(nameof(Busy));
            Set(ref _error, error, nameof(Error));
            if (error.Length > 0)
                OperationFailed?.Invoke(error);
        });
    }

    internal virtual void Update(NetworkInfo info)
    {
        var old = _info;
        _info = info;
        Changed(old.Name != info.Name, nameof(Name));
        Changed(old.Known != info.Known, nameof(Saved));
        Changed(old.State != info.State, nameof(State));
        Changed(old.State == ConnectionState.Connected != Connected, nameof(Connected));
        SavedProfiles.Sync(info.SavedProfiles ?? [], p => p.Id, i => i.Id, i => new WifiProfile(this, i),
            (p, i) => p.Update(i), p => p.Remove());
    }

    internal void Remove()
    {
        Removed = true;
        foreach (var profile in SavedProfiles)
            profile.Remove();
    }
}
