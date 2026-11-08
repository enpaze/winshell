namespace Winshell.Media;

/// <summary>Whether a <see cref="MediaSession"/> is playing.</summary>
public enum MediaPlaybackState
{
    /// <summary>Nothing is playing or paused, or Windows reports the session closed or only opened.</summary>
    [PublicAPI] Stopped = 0,

    /// <summary>The app is playing.</summary>
    [PublicAPI] Playing = 1,

    /// <summary>The app is paused.</summary>
    [PublicAPI] Paused = 2
}

/// <summary>What a <see cref="MediaSession"/> repeats.</summary>
public enum MediaLoopState
{
    /// <summary>Nothing repeats.</summary>
    [PublicAPI] None = 0,

    /// <summary>The track repeats.</summary>
    [PublicAPI] Track = 1,

    /// <summary>The whole list repeats.</summary>
    [PublicAPI] List = 2
}
