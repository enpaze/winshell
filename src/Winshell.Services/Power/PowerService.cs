namespace Winshell.Power;

public sealed class PowerDevice : ObservableObject
{
    private PowerDeviceInfo _info = PowerDeviceInfo.NoBattery;
    private bool _ready;

    internal PowerDevice()
    {
    }

    public PowerDeviceType Type => Read(_info).Type;

    public bool PowersTheSystem => Read(_info).PowersTheSystem;

    public double RemainingCapacity => Read(_info).RemainingCapacity;

    public double FullChargeCapacity => Read(_info).FullChargeCapacity;

    public double ChargeRate => Read(_info).ChargeRate;

    public double TimeToEmpty => Read(_info).TimeToEmpty;

    public double TimeToFull => Read(_info).TimeToFull;

    public double Percentage => Read(_info).Percentage;

    public bool IsPresent => Read(_info).IsPresent;

    public PowerDeviceState State => Read(_info).State;

    public double HealthPercentage => Read(_info).HealthPercentage;

    public bool HealthSupported => Read(_info).HealthSupported;

    public bool IsSystemBattery => Read(_info) is { Type: PowerDeviceType.Battery, PowersTheSystem: true };

    public string DeviceId => Read(_info).DeviceId;

    public string Model => Read(_info).Model;

    public bool Ready => Read(_ready);

    internal void Update(PowerDeviceInfo info, bool ready)
    {
        var old = _info;
        _info = info;
        Changed(old.Type != info.Type, nameof(Type));
        Changed(old.PowersTheSystem != info.PowersTheSystem, nameof(PowersTheSystem));
        Changed(old.RemainingCapacity != info.RemainingCapacity, nameof(RemainingCapacity));
        Changed(old.FullChargeCapacity != info.FullChargeCapacity, nameof(FullChargeCapacity));
        Changed(old.ChargeRate != info.ChargeRate, nameof(ChargeRate));
        Changed(old.TimeToEmpty != info.TimeToEmpty, nameof(TimeToEmpty));
        Changed(old.TimeToFull != info.TimeToFull, nameof(TimeToFull));
        Changed(old.Percentage != info.Percentage, nameof(Percentage));
        Changed(old.IsPresent != info.IsPresent, nameof(IsPresent));
        Changed(old.State != info.State, nameof(State));
        Changed(old.HealthPercentage != info.HealthPercentage, nameof(HealthPercentage));
        Changed(old.HealthSupported != HealthSupported, nameof(HealthSupported));
        Changed(old is { Type: PowerDeviceType.Battery, PowersTheSystem: true } != IsSystemBattery,
            nameof(IsSystemBattery));
        Changed(old.DeviceId != info.DeviceId, nameof(DeviceId));
        Changed(old.Model != info.Model, nameof(Model));
        Set(ref _ready, ready, nameof(Ready));
    }

    public override string ToString()
    {
        return $"PowerDevice({(_info.DeviceId.Length > 0 ? _info.DeviceId : "DisplayDevice")})";
    }
}

public sealed class PowerService : ObservableObject, IDisposable
{
    private readonly IPowerBackend _backend;
    private readonly StateReader<PowerSnapshot> _reader;
    private bool _onBattery;

    internal PowerService(IEventLoop loop, Log log, IPowerBackend? backend = null)
    {
        _backend = backend ?? new PowerBackend(log);
        _reader = new StateReader<PowerSnapshot>(_backend.ReadAsync, Apply, loop,
            new MonitorWarning(log, "power"));
        _backend.Changed += _reader.Request;
        _reader.Request();
    }

    public PowerDevice DisplayDevice { get => Read(field); } = new();

    public ObservableList<PowerDevice> Devices { get => Read(field); } = [];

    public bool OnBattery => Read(_onBattery);

    private void Apply(PowerSnapshot snapshot)
    {
        DisplayDevice.Update(snapshot.Display, snapshot.Ready);
        Set(ref _onBattery, snapshot.OnBattery, nameof(OnBattery));
        Devices.Sync(snapshot.Devices, d => d.DeviceId, i => i.DeviceId, _ => new PowerDevice(),
            (d, i) => d.Update(i, true));
    }

    public void Dispose()
    {
        _backend.Changed -= _reader.Request;
        _reader.Stop(_backend.Dispose);
    }
}

public sealed class PowerModeService : ObservableObject, IDisposable
{
    private readonly IPowerModeBackend _backend;
    private readonly StateReader<PowerModeSnapshot> _reader;
    private readonly Log _log;
    private PowerModeLevel _mode = PowerModeLevel.Unknown;
    private EffectivePowerMode _effective = EffectivePowerMode.Unknown;
    private bool _available;
    private bool _ready;

    internal PowerModeService(IEventLoop loop, Log log, IPowerModeBackend? backend = null)
    {
        _log = log;
        _backend = backend ?? new PowerModeBackend(log);
        _reader = new StateReader<PowerModeSnapshot>(_backend.ReadAsync, Apply, loop,
            new MonitorWarning(log, "power"));
        _backend.Changed += _reader.Request;
        _reader.Request();
    }

    public PowerModeLevel Mode
    {
        get => Read(_mode);
        set
        {
            if (value is not (PowerModeLevel.BestEfficiency or PowerModeLevel.Balanced
                or PowerModeLevel.BestPerformance))
            {
                _log.Warn("power", $"Cannot set the power mode to {(int)value}, which is not a slider position.");
                return;
            }

            var error = _backend.Set(value);
            if (error != 0)
                _log.Warn("power", $"Windows did not set the power mode (error {error}).");
        }
    }

    public EffectivePowerMode Effective => Read(_effective);

    public bool Available => Read(_available);

    public bool Ready => Read(_ready);

    private void Apply(PowerModeSnapshot snapshot)
    {
        Set(ref _mode, snapshot.Mode, nameof(Mode));
        Set(ref _effective, snapshot.Effective, nameof(Effective));
        Set(ref _available, snapshot.Available, nameof(Available));
        Set(ref _ready, snapshot.Ready, nameof(Ready));
    }

    public void Dispose()
    {
        _backend.Changed -= _reader.Request;
        _reader.Stop(_backend.Dispose);
    }
}
