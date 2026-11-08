namespace Winshell.Capture;

/// <summary>
/// <c>--fps</c>: keeps at most one frame in each <c>1 / fps</c> slot of a recording, the first that lands in it, by
/// its own timestamp. Without a cap it keeps every frame.
/// </summary>
internal sealed class FrameCap(int? fps)
{
    private long _lastSlot = -1;

    /// <summary>Whether to keep a frame captured <paramref name="at"/> after the recording started.</summary>
    public bool Keep(TimeSpan at)
    {
        if (fps is not { } rate)
            return true;
        var slot = at.Ticks * rate / TimeSpan.TicksPerSecond;
        if (slot <= _lastSlot)
            return false;
        _lastSlot = slot;
        return true;
    }
}

/// <summary>
/// Paces frames that arrive whenever the screen changes into a video at a constant rate. Video frame <c>k</c> starts at
/// <c>k / rate</c> seconds and shows the last frame captured before frame <c>k + 1</c> starts. So a frame repeats until
/// a newer one replaces it, of several in one video frame only the last is shown, and the video frames before the first
/// capture show that capture.
/// </summary>
/// <param name="rate">Video frames a second.</param>
/// <param name="show">Shows a frame for a number of video frames, in order. Their sum is the video's frame count.</param>
internal sealed class ConstantRate<T>(int rate, Action<T, int> show)
{
    private T? _pending;
    private bool _hasPending;
    private long _from;

    /// <summary>The video frames <paramref name="length"/> takes, rounded up, and at least one.</summary>
    private long Frames(TimeSpan length)
    {
        return Math.Max(1, (length.Ticks * rate + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond);
    }

    /// <summary>A frame captured <paramref name="at"/> after the recording started. Frames come in the order they were captured.</summary>
    public void Add(T frame, TimeSpan at)
    {
        var slot = at.Ticks * rate / TimeSpan.TicksPerSecond;
        if (_hasPending && slot > _from)
        {
            show(_pending!, (int)(slot - _from));
            _from = slot;
        }

        _pending = frame;
        _hasPending = true;
    }

    /// <summary>
    /// Shows the last frame until the video is <paramref name="length"/> long, or for one video frame when a frame came
    /// later than that. Nothing is shown when no frame came.
    /// </summary>
    public void Finish(TimeSpan length)
    {
        if (_hasPending)
            show(_pending!, (int)Math.Max(1, Frames(length) - _from));
        _hasPending = false;
        _pending = default;
    }
}
