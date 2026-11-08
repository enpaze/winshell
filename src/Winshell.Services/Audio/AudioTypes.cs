namespace Winshell.Audio;

/// <summary>What an <see cref="AudioNode"/> is.</summary>
internal enum AudioNodeType
{
    PlaybackDevice,
    RecordingDevice,
    PlaybackSession,
    RecordingSession
}

/// <summary>A speaker position, in the order of the Windows speaker mask. Without a mask, one channel is Mono, two are the front pair, and more are Unknown.</summary>
public enum AudioChannel
{
    /// <summary>A channel whose position Windows does not give.</summary>
    [PublicAPI] Unknown,

    /// <summary>The only channel of a mono stream.</summary>
    [PublicAPI] Mono,

    /// <summary>Front left.</summary>
    [PublicAPI] FrontLeft,

    /// <summary>Front right.</summary>
    [PublicAPI] FrontRight,

    /// <summary>Front center.</summary>
    [PublicAPI] FrontCenter,

    /// <summary>The subwoofer, for low-frequency effects.</summary>
    [PublicAPI] LowFrequency,

    /// <summary>Back left.</summary>
    [PublicAPI] BackLeft,

    /// <summary>Back right.</summary>
    [PublicAPI] BackRight,

    /// <summary>Front, between left and center.</summary>
    [PublicAPI] FrontLeftOfCenter,

    /// <summary>Front, between right and center.</summary>
    [PublicAPI] FrontRightOfCenter,

    /// <summary>Back center.</summary>
    [PublicAPI] BackCenter,

    /// <summary>Side left.</summary>
    [PublicAPI] SideLeft,

    /// <summary>Side right.</summary>
    [PublicAPI] SideRight,

    /// <summary>Above the listener.</summary>
    [PublicAPI] TopCenter,

    /// <summary>Top front left.</summary>
    [PublicAPI] TopFrontLeft,

    /// <summary>Top front center.</summary>
    [PublicAPI] TopFrontCenter,

    /// <summary>Top front right.</summary>
    [PublicAPI] TopFrontRight,

    /// <summary>Top back left.</summary>
    [PublicAPI] TopBackLeft,

    /// <summary>Top back center.</summary>
    [PublicAPI] TopBackCenter,

    /// <summary>Top back right.</summary>
    [PublicAPI] TopBackRight
}
