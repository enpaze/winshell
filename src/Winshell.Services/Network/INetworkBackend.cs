namespace Winshell.Network;

internal interface INetworkBackend : IDisposable
{
    /// <summary>Windows reported a change, or an action changed something. Raised on any thread.</summary>
    event Action? Changed;

    /// <summary>
    /// Reads the adapters, profiles, radios and connectivity. The first read starts watching Windows until the backend
    /// is disposed.
    /// </summary>
    Task<NetworkSnapshot> ReadAsync(CancellationToken cancellation);

    Task RequestAccessAsync();

    /// <summary>Null when the scan ran. The request-access failure is that string, not a throw.</summary>
    Task<string?> ScanAsync(Guid device);
    Task<ConnectionFailReason?> ConnectAsync(Guid device, string ssid, string? password);

    /// <summary>Disconnects a Wi-Fi adapter. Null when Windows accepted it.</summary>
    Task<string?> DisconnectAsync(Guid device);

    /// <summary>Deletes a saved profile by its name. Null when Windows deleted it.</summary>
    Task<string?> ForgetAsync(Guid device, string profileName);

    /// <summary>Sets the profile's connection mode. Null when Windows accepted it or it was already that mode.</summary>
    Task<string?> SetProfileAutoConnectAsync(Guid device, string profileName, bool autoConnect);

    /// <summary>Removes the profile's key element. Null when Windows accepted it or there was no key.</summary>
    Task<string?> ClearProfilePasswordAsync(Guid device, string profileName);

    /// <summary>Connects with a saved profile. Null when Windows accepts the request.</summary>
    Task<string?> ConnectProfileAsync(Guid device, string profileName, bool hidden);
}
