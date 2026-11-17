namespace Winshell.Network;

public abstract class NetworkDevice : ObservableObject
{
    private NetworkDeviceInfo _info;

    internal NetworkDevice(NetworkDeviceInfo info, IEventLoop loop, INetworkBackend backend)
    {
        _info = info;
        Loop = loop;
        Backend = backend;
    }

    internal IEventLoop Loop { get; }

    internal INetworkBackend Backend { get; }

    internal Guid NativeId => _info.Id;

    internal NetworkDeviceInfo Info => _info;

    internal bool Removed { get; private set; }

    public string Id => Read(_info).Id.ToString();

    public DeviceType Type => Read(_info).Type;

    public string Name => Read(_info).Name;

    public ObservableList<Network> Networks { get => Read(field); } = [];

    public string Address => Read(_info).Address;

    public bool Connected => Read(_info).State == ConnectionState.Connected;

    public ConnectionState State => Read(_info).State;

    internal virtual void Update(NetworkDeviceInfo info)
    {
        var old = _info;
        _info = info;
        Changed(old.Name != info.Name, nameof(Name));
        Changed(old.Address != info.Address, nameof(Address));
        Changed(old.State != info.State, nameof(State));
        Changed(old.State == ConnectionState.Connected != Connected, nameof(Connected));
        Networks.Sync(info.Networks, n => n.Name, i => i.Name,
            i => Type == DeviceType.Wifi ? new WifiNetwork(this, i) : new WiredNetwork(this, i),
            (n, i) => n.Update(i),
            n => n.Remove());
    }

    internal void Remove()
    {
        Removed = true;
        foreach (var network in Networks)
            network.Remove();
    }
}
