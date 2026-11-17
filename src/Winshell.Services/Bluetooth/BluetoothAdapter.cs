namespace Winshell.Bluetooth;

public sealed class BluetoothAdapter : ObservableObject
{
    private readonly BluetoothService _service;
    private BluetoothAdapterInfo _info;
    private string _error = "";

    internal BluetoothAdapter(BluetoothService service, BluetoothAdapterInfo info)
    {
        _service = service;
        _info = info;
    }

    public event Action<string>? OperationFailed;

    public string Id => Read(_info).Id;

    public string Name => Read(_info).Name;

    public string Address => Read(_info).Address;

    public BluetoothAdapterState State => Read(_info).State;

    public bool Enabled
    {
        get => Read(_info).State == BluetoothAdapterState.On;
        set => _ = _service.SetAdapterEnabledAsync(this, value);
    }

    public bool Discoverable
    {
        get => Read(_info).Discoverable;
        set => _ = _service.SetDiscoverableAsync(this, value);
    }

    public bool Pairable
    {
        get => Read(_info).Pairable;
        set => _ = _service.SetPairableAsync(this, value);
    }

    public string Error => Read(_error);

    internal bool ReportsOn => _info.State == BluetoothAdapterState.On;

    internal void Report(string error)
    {
        Set(ref _error, error, nameof(Error));
        if (error.Length > 0)
            OperationFailed?.Invoke(error);
    }

    internal void Update(BluetoothAdapterInfo info)
    {
        var old = _info;
        _info = info;
        Changed(old.Name != info.Name, nameof(Name));
        Changed(old.Address != info.Address, nameof(Address));
        Changed(old.Discoverable != info.Discoverable, nameof(Discoverable));
        Changed(old.Pairable != info.Pairable, nameof(Pairable));
        Changed(old.State != info.State, nameof(State));
        Changed(old.State == BluetoothAdapterState.On != ReportsOn, nameof(Enabled));
        if (old.State != info.State || old.Discoverable != info.Discoverable || old.Pairable != info.Pairable)
            Set(ref _error, "", nameof(Error));
    }
}
