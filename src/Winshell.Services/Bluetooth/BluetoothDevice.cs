namespace Winshell.Bluetooth;

public sealed class BluetoothDevice : ObservableObject
{
    private readonly BluetoothService _service;
    private readonly Lock _pairingLock = new();
    private readonly DeviceAction _action;
    private BluetoothDeviceInfo _info;
    private BluetoothOperation _operation;
    private string _error = "";
    private CancellationTokenSource? _pairing;
    private bool _removed;

    internal BluetoothDevice(BluetoothService service, BluetoothDeviceInfo info, BluetoothAdapter adapter)
    {
        _service = service;
        _info = info;
        Adapter = adapter;
        _action = new DeviceAction(service.Post);
    }

    public event Action<string>? OperationFailed;

    internal string Key => _info.Key;

    public string Id => Read(_info).Id;

    public BluetoothAdapter Adapter { get => Read(field); }

    public string Address => Read(_info).Address;

    public string Name => Read(_info).Name;

    public string Icon => Read(_info).Icon;

    public bool Paired => Read(_info).Paired;

    public bool Present => Read(_info).Present;

    public bool CanPair => Read(_info) is { Paired: false, CanPair: true };

    public bool Connected => Read(_info).Connected;

    public BluetoothDeviceState State =>
        Read(_info).Connected ? BluetoothDeviceState.Connected : BluetoothDeviceState.Disconnected;

    public bool BatteryAvailable => Read(_info).Battery.HasValue;

    public double Battery => Read(_info).Battery ?? 0;

    public BluetoothOperation Operation => Read(_operation);

    public bool Busy => Read(_operation) != BluetoothOperation.None;

    public bool Pairing => Read(_operation) == BluetoothOperation.Pairing;

    public string Error => Read(_error);

    public void Connect()
    {
        _ = SetConnectionAsync(true);
    }

    public void Disconnect()
    {
        _ = SetConnectionAsync(false);
    }

    public void Pair()
    {
        _ = PairAsync();
    }

    public void CancelPairing()
    {
        lock (_pairingLock)
        {
            _pairing?.Cancel();
        }
    }

    public void Unpair()
    {
        _ = UnpairAsync();
    }

    internal Task SetConnectionAsync(bool connect)
    {
        return RunAsync(connect ? BluetoothOperation.Connecting : BluetoothOperation.Disconnecting, async info =>
        {
            if (!info.Paired || info.Container is not { } container)
                return "Windows connection requests require a paired Bluetooth audio device with a device container.";
            return await _service.Backend.SetAudioConnectionAsync(container, connect).ConfigureAwait(false)
                ? null
                : "Windows accepted no Bluetooth audio connection request. Other device types reconnect through Windows.";
        });
    }

    internal Task PairAsync()
    {
        return RunAsync(BluetoothOperation.Pairing, async info =>
        {
            if (info.Paired)
                return null;
            if (!info.CanPair)
                return "Windows reports that this device cannot be paired.";
            var cancellation = new CancellationTokenSource();
            lock (_pairingLock)
            {
                _pairing = cancellation;
            }

            try
            {
                return await _service.Backend.PairAsync(info.Id, cancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                lock (_pairingLock)
                {
                    _pairing = null;
                    cancellation.Dispose();
                }
            }
        });
    }

    internal Task UnpairAsync()
    {
        return RunAsync(BluetoothOperation.Unpairing, async info =>
        {
            foreach (var id in info.PairedIds)
            {
                var error = await _service.Backend.UnpairAsync(id).ConfigureAwait(false);
                if (error is not null)
                    return error;
            }

            return null;
        });
    }

    private Task RunAsync(BluetoothOperation operation, Func<BluetoothDeviceInfo, Task<string?>> action)
    {
        if (_removed)
            return Task.CompletedTask;
        return _action.RunAsync(() =>
        {
            SetOperation(operation);
            Set(ref _error, "", nameof(Error));
            _service.Invalidate();
        }, async () =>
        {
            try
            {
                return await action(_info).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return "Cancelled";
            }
        }, error =>
        {
            _service.Invalidate();
            if (!_removed)
            {
                Set(ref _error, error, nameof(Error));
                if (error.Length > 0)
                    OperationFailed?.Invoke(error);
            }

            _service.Refresh();
        }, () =>
        {
            if (!_removed)
                SetOperation(BluetoothOperation.None);
        });
    }

    private void SetOperation(BluetoothOperation operation)
    {
        var wasPairing = Pairing;
        var wasBusy = Busy;
        if (!Set(ref _operation, operation, nameof(Operation)))
            return;
        Changed(wasBusy != Busy, nameof(Busy));
        Changed(wasPairing != Pairing, nameof(Pairing));
    }

    internal void Update(BluetoothDeviceInfo info)
    {
        var old = _info;
        _info = info;
        Changed(old.Id != info.Id, nameof(Id));
        Changed(old.Address != info.Address, nameof(Address));
        Changed(old.Name != info.Name, nameof(Name));
        Changed(old.Icon != info.Icon, nameof(Icon));
        Changed(old.Paired != info.Paired, nameof(Paired));
        Changed(old is { Paired: false, CanPair: true } != CanPair, nameof(CanPair));
        Changed(old.Present != info.Present, nameof(Present));
        Changed(old.Connected != info.Connected, nameof(Connected));
        Changed(old.Connected != info.Connected, nameof(State));
        Changed(old.Battery.HasValue != info.Battery.HasValue, nameof(BatteryAvailable));
        Changed((old.Battery ?? 0) != (info.Battery ?? 0), nameof(Battery));
    }

    internal void Remove()
    {
        _removed = true;
        CancelPairing();
    }
}
