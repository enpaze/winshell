namespace Winshell.Network;

public sealed class NetworkProfile : ObservableObject
{
    private NetworkProfileInfo _info;

    internal NetworkProfile(NetworkProfileInfo info)
    {
        _info = info;
    }

    public string Id => Read(_info).Id;

    public string Name => Read(_info).Name;

    public string AdapterId => Read(_info).AdapterId.ToString();

    public NetworkConnectivity Connectivity => Read(_info).Connectivity;

    public NetworkCost Cost => Read(_info).Cost;

    public bool Metered => IsMetered(Read(_info));

    public bool Roaming => Read(_info).Roaming;

    public bool OverDataLimit => Read(_info).OverDataLimit;

    public bool SignalAvailable => Read(_info).SignalStrength.HasValue;

    public double SignalStrength => Read(_info).SignalStrength ?? 0;

    private static bool IsMetered(NetworkProfileInfo info)
    {
        return info.Cost is NetworkCost.Fixed or NetworkCost.Variable || info.Roaming || info.OverDataLimit;
    }

    internal void Update(NetworkProfileInfo info)
    {
        var old = _info;
        _info = info;
        Changed(old.Name != info.Name, nameof(Name));
        Changed(old.AdapterId != info.AdapterId, nameof(AdapterId));
        Changed(old.Connectivity != info.Connectivity, nameof(Connectivity));
        Changed(old.Cost != info.Cost, nameof(Cost));
        Changed(IsMetered(old) != IsMetered(info), nameof(Metered));
        Changed(old.Roaming != info.Roaming, nameof(Roaming));
        Changed(old.OverDataLimit != info.OverDataLimit, nameof(OverDataLimit));
        Changed(old.SignalStrength.HasValue != info.SignalStrength.HasValue, nameof(SignalAvailable));
        Changed((old.SignalStrength ?? 0) != (info.SignalStrength ?? 0), nameof(SignalStrength));
    }
}
