using Winshell.Native;

namespace Winshell.Power;

internal sealed record PowerModeSnapshot(bool Ready, PowerModeLevel Mode, EffectivePowerMode Effective, bool Available)
{
    public static readonly PowerModeSnapshot Initial = new(false, PowerModeLevel.Unknown, EffectivePowerMode.Unknown,
        false);
}

internal interface IPowerModeBackend : IDisposable
{
    event Action? Changed;

    Task<PowerModeSnapshot> ReadAsync(CancellationToken cancellation);

    uint Set(PowerModeLevel mode);
}

internal sealed class PowerModeBackend(Log log) : IPowerModeBackend
{
    private int _effective = (int)EffectivePowerMode.Unknown;
    private bool _watching;

    public event Action? Changed;

    public Task<PowerModeSnapshot> ReadAsync(CancellationToken cancellation)
    {
        return Task.Run(Read, cancellation);
    }

    private PowerModeSnapshot Read()
    {
        if (!_watching)
        {
            _watching = true;
            if (!PowerMode.ListenEffective(mode =>
                {
                    Volatile.Write(ref _effective, mode);
                    Changed?.Invoke();
                }, e => log.Warn("power", e.Message)))
                log.Warn("power", "This Windows build has no power mode notifications.");
        }

        var effective = EffectiveFrom(Volatile.Read(ref _effective));
        var error = PowerMode.GetUserConfigured(PowerBackend.OnBattery(), out var guid);
        return new PowerModeSnapshot(true, LevelFrom(error, guid), effective, error == 0);
    }

    public uint Set(PowerModeLevel mode)
    {
        var result = PowerMode.SetUserConfigured(PowerBackend.OnBattery(), ToGuid(mode));
        Changed?.Invoke();
        return result;
    }

    public void Dispose()
    {
        if (_watching)
            PowerMode.StopListeningEffective();
        _watching = false;
    }

    internal static PowerModeLevel LevelFrom(uint error, Guid guid)
    {
        return error == 0 ? FromGuid(guid) : PowerModeLevel.Unknown;
    }

    internal static PowerModeLevel FromGuid(Guid guid)
    {
        if (guid == PowerMode.BestEfficiency)
            return PowerModeLevel.BestEfficiency;
        if (guid == PowerMode.BestPerformance || guid == PowerMode.DeprecatedPerformance)
            return PowerModeLevel.BestPerformance;
        return PowerModeLevel.Balanced;
    }

    internal static Guid ToGuid(PowerModeLevel mode)
    {
        return mode switch
        {
            PowerModeLevel.BestEfficiency => PowerMode.BestEfficiency,
            PowerModeLevel.BestPerformance => PowerMode.BestPerformance,
            _ => Guid.Empty
        };
    }

    internal static EffectivePowerMode EffectiveFrom(int mode)
    {
        var value = (EffectivePowerMode)mode;
        return Enum.IsDefined(value) ? value : EffectivePowerMode.Balanced;
    }
}
