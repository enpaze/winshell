namespace Winshell.Capture;

internal sealed class FrameCap(int? fps)
{
    private long _lastSlot = -1;

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

internal sealed class ConstantRate<T>(int rate, Action<T, int> show)
{
    private T? _pending;
    private bool _hasPending;
    private long _from;

    private long Frames(TimeSpan length)
    {
        return Math.Max(1, (length.Ticks * rate + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond);
    }

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

    public void Finish(TimeSpan length)
    {
        if (_hasPending)
            show(_pending!, (int)Math.Max(1, Frames(length) - _from));
        _hasPending = false;
        _pending = default;
    }
}
