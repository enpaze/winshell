namespace Winshell.Network;

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

    public string Name => Read(_info).Name;

    public NetworkDevice Device { get => Read(field); }

    public bool Connected => Read(_info).State == ConnectionState.Connected;

    public bool Saved => Read(_info).Known;

    public ConnectionState State => Read(_info).State;

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
