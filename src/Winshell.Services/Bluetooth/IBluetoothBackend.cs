namespace Winshell.Bluetooth;

internal interface IBluetoothBackend : IDisposable
{
    event Action? Changed;

    void StartWatching();

    void StopWatching();

    Task<BluetoothSnapshot> ReadAsync(CancellationToken cancellation);

    void StartDiscovery();
    void StopDiscovery();
    Task<string?> PairAsync(string id, CancellationToken cancellation);
    Task<string?> UnpairAsync(string id);
    Task<bool> SetAudioConnectionAsync(Guid container, bool connect);

    Task<string?> SetDiscoverableAsync(bool discoverable);

    Task<string?> SetPairableAsync(bool pairable);
}
