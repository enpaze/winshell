namespace Winshell.Network;

/// <summary>
/// A network of an adapter. A wired one is a <see cref="WiredNetwork"/> and a Wi-Fi one is a
/// <see cref="WifiNetwork"/>.
/// </summary>
public abstract class Network : ObservableObject
{
    private NetworkInfo _info;

    internal Network(NetworkDevice device, NetworkInfo info)
    {
        Device = device;
        _info = info;
    }

    internal NetworkInfo Info => _info;

    internal bool Removed { get; private set; }

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

    /// <summary>
    /// The saved Wi-Fi profiles for this network: those whose SSID is its name, and those that can't be read whose
    /// profile name is. Empty for a wired network.
    /// </summary>
    public ObservableList<WifiProfile> SavedProfiles { get => Read(field); } = [];

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
