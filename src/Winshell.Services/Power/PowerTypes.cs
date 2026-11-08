namespace Winshell.Power;

/// <summary>What a battery is doing.</summary>
public enum PowerDeviceState
{
    /// <summary>Windows does not say, or there is no battery.</summary>
    [PublicAPI] Unknown = 0,

    /// <summary>The battery is charging.</summary>
    [PublicAPI] Charging = 1,

    /// <summary>The battery is discharging.</summary>
    [PublicAPI] Discharging = 2,

    /// <summary>The battery is discharging and has no charge left.</summary>
    [PublicAPI] Empty = 3,

    /// <summary>The PC is on line power and the battery is full.</summary>
    [PublicAPI] FullyCharged = 4,

    /// <summary>The PC is on line power and the battery is not charging, though it is not full.</summary>
    [PublicAPI] NotCharging = 5
}

/// <summary>The kind of a <see cref="PowerDevice"/>.</summary>
public enum PowerDeviceType
{
    /// <summary>No battery: the PC has none, or none is present.</summary>
    [PublicAPI] Unknown = 0,

    /// <summary>A battery.</summary>
    [PublicAPI] Battery = 2
}

/// <summary>The three positions of the Windows power mode slider.</summary>
public enum PowerModeLevel
{
    /// <summary>Best power efficiency.</summary>
    [PublicAPI] BestEfficiency = 0,

    /// <summary>Balanced.</summary>
    [PublicAPI] Balanced = 1,

    /// <summary>Best performance.</summary>
    [PublicAPI] BestPerformance = 2
}

/// <summary>The power mode Windows is applying, including energy saver and game mode.</summary>
public enum EffectivePowerMode
{
    /// <summary>Energy saver is on.</summary>
    [PublicAPI] EnergySaver = 0,

    /// <summary>Favors battery life over performance.</summary>
    [PublicAPI] BetterBattery = 1,

    /// <summary>Balances performance and power use.</summary>
    [PublicAPI] Balanced = 2,

    /// <summary>Favors performance over power use.</summary>
    [PublicAPI] HighPerformance = 3,

    /// <summary>Favors performance the most.</summary>
    [PublicAPI] MaxPerformance = 4,

    /// <summary>Game mode is on.</summary>
    [PublicAPI] GameMode = 5,

    /// <summary>Windows Mixed Reality is running.</summary>
    [PublicAPI] MixedReality = 6
}
