namespace Winshell.Network;

/// <summary>A wired network. Windows manages its connection; it has no Wi-Fi command.</summary>
public sealed class WiredNetwork : Network
{
    internal WiredNetwork(NetworkDevice device, NetworkInfo info)
        : base(device, info)
    {
    }
}
