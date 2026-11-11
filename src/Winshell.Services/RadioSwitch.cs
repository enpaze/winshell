using Windows.Devices.Radios;

namespace Winshell;

/// <summary>What asking Windows to change a radio came to. The feature writes the sentence.</summary>
internal enum RadioSwitchStatus
{
    /// <summary>Every radio that was asked reports the requested state.</summary>
    Done,

    /// <summary>The reading had no radio of the requested kind.</summary>
    Missing,

    /// <summary>The user denied radio control.</summary>
    DeniedByUser,

    /// <summary>Windows or its policy denied radio control.</summary>
    DeniedBySystem,

    /// <summary>Windows did not allow radio control.</summary>
    NotAllowed,

    /// <summary>Windows accepted the request and left the radio where it was.</summary>
    Unchanged
}

/// <summary>A radio a backend read. <see cref="RadioSwitch"/> asks Windows to change it.</summary>
internal interface IDeviceRadio
{
    RadioKind Kind { get; }
    RadioState State { get; }

    /// <summary>The process-wide radio-control request. It can show Windows' consent UI.</summary>
    Task<RadioAccessStatus> RequestAccessAsync();

    Task<RadioAccessStatus> SetStateAsync(RadioState state);
}

/// <summary>
/// The radios a backend read for one device, and their state at that read. Two readings are equal when their states
/// are: Windows hands out new radio objects on every read, and they are only how to change that state.
/// </summary>
internal sealed record RadioReading(IReadOnlyList<IDeviceRadio> Radios, RadioState State)
{
    public static readonly RadioReading None = new([], RadioState.Unknown);

    public bool Equals(RadioReading? other)
    {
        return other is not null && State == other.State;
    }

    public override int GetHashCode()
    {
        return State.GetHashCode();
    }

    /// <summary>
    /// The radios taken together: <see cref="RadioState.On"/> when any is on, else <see cref="RadioState.Off"/> when
    /// any can be turned on, else <see cref="RadioState.Disabled"/> when any is held off, else
    /// <see cref="RadioState.Unknown"/>.
    /// </summary>
    public static RadioReading Read(IReadOnlyList<IDeviceRadio> radios)
    {
        var states = radios.Select(static radio => radio.State).ToHashSet();
        var state = states.Contains(RadioState.On) ? RadioState.On
            : states.Contains(RadioState.Off) ? RadioState.Off
            : states.Contains(RadioState.Disabled) ? RadioState.Disabled
            : RadioState.Unknown;
        return new RadioReading(radios, state);
    }

    /// <summary>The four states, named by the caller's enum. On, off, disabled, then unknown.</summary>
    public T Map<T>(T on, T off, T disabled, T unknown)
    {
        return State switch
        {
            RadioState.On => on,
            RadioState.Off => off,
            RadioState.Disabled => disabled,
            _ => unknown
        };
    }
}

/// <summary>
/// A Windows <see cref="Radio"/>. The only production caller of <see cref="Radio.SetStateAsync"/> and
/// <see cref="Radio.RequestAccessAsync"/>.
/// </summary>
internal sealed class WindowsRadio(Radio radio) : IDeviceRadio
{
    public RadioKind Kind => radio.Kind;
    public RadioState State => radio.State;

    public async Task<RadioAccessStatus> RequestAccessAsync()
    {
        return await Radio.RequestAccessAsync();
    }

    public Task<RadioAccessStatus> SetStateAsync(RadioState state)
    {
        return radio.SetStateAsync(state).AsTask();
    }
}

/// <summary>
/// Asks Windows to turn radios on or off: <see cref="Radio.RequestAccessAsync"/>, then
/// <see cref="Radio.SetStateAsync"/>. An allowed request can still leave a radio where hardware or policy put it.
/// The result is a status. The feature writes the sentence for it.
/// </summary>
internal static class RadioSwitch
{
    internal const string Unchanged = "Windows did not change the radio.";

    /// <summary>The sentence for <paramref name="status"/>. <paramref name="missing"/> is that feature's no-radio text.</summary>
    public static string? Describe(RadioSwitchStatus status, string missing)
    {
        return status switch
        {
            RadioSwitchStatus.Done => null,
            RadioSwitchStatus.Missing => missing,
            RadioSwitchStatus.DeniedByUser => "The user denied radio control.",
            RadioSwitchStatus.DeniedBySystem => "Windows or its policy denied radio control.",
            RadioSwitchStatus.NotAllowed => "Windows did not allow radio control.",
            RadioSwitchStatus.Unchanged => Unchanged,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    /// <summary>
    /// The radios of <paramref name="kind"/> among <paramref name="radios"/>, leaving every other radio alone.
    /// <see cref="RadioSwitchStatus.Done"/> when each of them reports <paramref name="enabled"/>.
    /// </summary>
    public static async Task<RadioSwitchStatus> SetEnabledAsync(RadioKind kind, IReadOnlyList<IDeviceRadio> radios,
        bool enabled)
    {
        var asked = radios.Where(radio => radio.Kind == kind).ToList();
        if (asked.Count == 0)
            return RadioSwitchStatus.Missing;
        // Already there: do not request access. That call can prompt, and nothing would change.
        if (!asked.Any(radio => NeedsChange(radio, enabled)))
            return RadioSwitchStatus.Done;

        // One process-wide request. The radio is only how that request is reached.
        var allowed = await asked[0].RequestAccessAsync().ConfigureAwait(false);
        if (allowed != RadioAccessStatus.Allowed)
            return Denied(allowed);

        var desired = enabled ? RadioState.On : RadioState.Off;
        foreach (var radio in asked)
        {
            if (!NeedsChange(radio, enabled))
                continue;
            var result = await radio.SetStateAsync(desired).ConfigureAwait(false);
            if (result != RadioAccessStatus.Allowed)
                return Denied(result);
            // Allowed means Windows accepted the request. Hardware or policy can leave the previous state.
            if (radio.State != desired)
                return RadioSwitchStatus.Unchanged;
        }

        return RadioSwitchStatus.Done;
    }

    private static RadioSwitchStatus Denied(RadioAccessStatus status)
    {
        return status switch
        {
            RadioAccessStatus.DeniedByUser => RadioSwitchStatus.DeniedByUser,
            RadioAccessStatus.DeniedBySystem => RadioSwitchStatus.DeniedBySystem,
            _ => RadioSwitchStatus.NotAllowed
        };
    }

    // Turning off skips a radio that is not on. Disabled is not a state an app can request.
    // Turning on asks every radio that is not on, including a disabled one, which may refuse.
    private static bool NeedsChange(IDeviceRadio radio, bool turnOn)
    {
        return turnOn ? radio.State != RadioState.On : radio.State == RadioState.On;
    }
}
