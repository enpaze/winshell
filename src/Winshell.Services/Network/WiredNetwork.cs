namespace Winshell.Network;

public sealed class WiredNetwork : Network
{
    internal WiredNetwork(NetworkDevice device, NetworkInfo info)
        : base(device, info)
    {
    }
}
