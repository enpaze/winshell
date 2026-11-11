using Winshell.Native;

namespace Winshell.Power;

internal sealed record PowerModeSnapshot(bool Ready, PowerModeLevel Mode, EffectivePowerMode Effective, bool Available)
{
    public static readonly PowerModeSnapshot Initial = new(false, PowerModeLevel.Unknown, EffectivePowerMode.Unknown,
        false);
}

internal interface IPowerModeBackend : IDisposable
{
    /// <summary>Windows reported a change, or a write changed something. Raised on any thread.</summary>
    event Action? Changed;

    /// <summary>Reads the power mode. The first read starts watching Windows until the backend is disposed.</summary>
    Task<PowerModeSnapshot> ReadAsync(CancellationToken cancellation);

    /// <summary>
    /// Sets the slider for the supply in use: DC while on battery, AC otherwise. Returns the Windows error, or 0. This
    /// changes the power mode, not the power plan. <paramref name="mode"/> is a slider position; the service checks.
    /// </summary>
    uint Set(PowerModeLevel mode);
}

/// <summary>
/// The Windows power mode (Settings, System, Power). The slider is the public user-configured AC or DC
/// mode. The effective mode is what Windows is applying, including energy saver and game mode.
/// </summary>
/// <param name="log">Where a failed power mode callback is reported.</param>
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
            // Windows calls back once at once with the effective mode, then on each change.
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

    /// <summary>
    /// The slider after a read. A failed read is unknown, including when the GUID came back empty. An empty GUID on
    /// success is Balanced, which is what Windows writes for that position.
    /// </summary>
    internal static PowerModeLevel LevelFrom(uint error, Guid guid)
    {
        return error == 0 ? FromGuid(guid) : PowerModeLevel.Unknown;
    }

    /// <summary>
    /// The slider GUID Windows returned. The deprecated performance GUID is best performance, and a write never sends
    /// it. Anything else, including an empty GUID, is Balanced.
    /// </summary>
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

    /// <summary>
    /// The effective mode Windows reported. No report yet is <see cref="EffectivePowerMode.Unknown"/>. A value this
    /// enum does not name stays Balanced.
    /// </summary>
    internal static EffectivePowerMode EffectiveFrom(int mode)
    {
        var value = (EffectivePowerMode)mode;
        return Enum.IsDefined(value) ? value : EffectivePowerMode.Balanced;
    }
}
