namespace Winshell.Network;

/// <summary>A Wi-Fi network in range.</summary>
public sealed class WifiNetwork : Network
{
    internal WifiNetwork(NetworkDevice device, NetworkInfo info)
        : base(device, info)
    {
    }

    /// <summary>The signal strength, from 0 to 1.</summary>
    public double SignalStrength => Read(Info).SignalStrength;

    /// <summary>How the network is secured.</summary>
    public WifiSecurityType Security => Read(Info).Security;

    /// <summary>Connects with a password, for a secured network that isn't saved. The password is not kept.</summary>
    public void ConnectWithPassword(string password)
    {
        _ = ConnectAsync(password);
    }

    internal override void Update(NetworkInfo info)
    {
        var old = Info;
        base.Update(info);
        // A stored reading: notify when the number changes, not when it passes a tolerance.
        Changed(old.SignalStrength != info.SignalStrength, nameof(SignalStrength));
        Changed(old.Security != info.Security, nameof(Security));
    }
}
