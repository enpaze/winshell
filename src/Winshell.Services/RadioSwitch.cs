using Windows.Devices.Radios;

namespace Winshell;

internal enum RadioSwitchStatus
{
    Done,

    Missing,

    DeniedByUser,

    DeniedBySystem,

    NotAllowed,

    Unchanged
}

internal interface IDeviceRadio
{
    RadioKind Kind { get; }
    RadioState State { get; }

    Task<RadioAccessStatus> RequestAccessAsync();

    Task<RadioAccessStatus> SetStateAsync(RadioState state);
}

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

    public static RadioReading Read(IReadOnlyList<IDeviceRadio> radios)
    {
        var states = radios.Select(static radio => radio.State).ToHashSet();
        var state = states.Contains(RadioState.On) ? RadioState.On
            : states.Contains(RadioState.Off) ? RadioState.Off
            : states.Contains(RadioState.Disabled) ? RadioState.Disabled
            : RadioState.Unknown;
        return new RadioReading(radios, state);
    }

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

internal static class RadioSwitch
{
    internal const string Unchanged = "Windows did not change the radio.";

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

    public static async Task<RadioSwitchStatus> SetEnabledAsync(RadioKind kind, IReadOnlyList<IDeviceRadio> radios,
        bool enabled)
    {
        var asked = radios.Where(radio => radio.Kind == kind).ToList();
        if (asked.Count == 0)
            return RadioSwitchStatus.Missing;
        if (!asked.Any(radio => NeedsChange(radio, enabled)))
            return RadioSwitchStatus.Done;

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

    private static bool NeedsChange(IDeviceRadio radio, bool turnOn)
    {
        return turnOn ? radio.State != RadioState.On : radio.State == RadioState.On;
    }
}
