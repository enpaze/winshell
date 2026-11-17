namespace Winshell.Audio;

internal enum AudioNodeType
{
    PlaybackDevice,
    RecordingDevice,
    PlaybackSession,
    RecordingSession
}

public enum AudioChannel
{
    Unknown,

    Mono,

    FrontLeft,

    FrontRight,

    FrontCenter,

    LowFrequency,

    BackLeft,

    BackRight,

    FrontLeftOfCenter,

    FrontRightOfCenter,

    BackCenter,

    SideLeft,

    SideRight,

    TopCenter,

    TopFrontLeft,

    TopFrontCenter,

    TopFrontRight,

    TopBackLeft,

    TopBackCenter,

    TopBackRight
}
