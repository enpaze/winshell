namespace Winshell.Network;

internal interface INetworkBackend : IDisposable
{
    event Action? Changed;

    Task<NetworkSnapshot> ReadAsync(CancellationToken cancellation);

    Task RequestAccessAsync();

    Task<string?> ScanAsync(Guid device);
    Task<ConnectionFailReason?> ConnectAsync(Guid device, string ssid, string? password);

    Task<string?> DisconnectAsync(Guid device);

    Task<string?> ForgetAsync(Guid device, string profileName);

    Task<string?> SetProfileAutoConnectAsync(Guid device, string profileName, bool autoConnect);

    Task<string?> ClearProfilePasswordAsync(Guid device, string profileName);

    Task<string?> ConnectProfileAsync(Guid device, string profileName, bool hidden);
}
