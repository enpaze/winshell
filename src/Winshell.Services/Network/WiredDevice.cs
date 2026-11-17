namespace Winshell.Network;

public sealed class WiredDevice : NetworkDevice
{
    internal WiredDevice(NetworkDeviceInfo info, IEventLoop loop, INetworkBackend backend)
        : base(info, loop, backend)
    {
    }

    public int LinkSpeed => (int)Math.Min(int.MaxValue, Read(Info).LinkSpeed);

    public bool HasLink => Read(Info).HasLink;

    internal override void Update(NetworkDeviceInfo info)
    {
        var old = Info;
        base.Update(info);
        Changed(old.LinkSpeed != info.LinkSpeed, nameof(LinkSpeed));
        Changed(old.HasLink != info.HasLink, nameof(HasLink));
    }
}
