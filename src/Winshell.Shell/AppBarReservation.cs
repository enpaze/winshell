using Winshell.Native;

namespace Winshell;

internal sealed class AppBarReservation
{
    private RECT _lastRequest;
    private bool _requested;
    private bool _shortGrantStale;

    public RECT Granted { get; private set; }

    public bool Reserve(RECT desired, Func<RECT, RECT> setPos)
    {
        var repeated = _requested && desired.Equals(_lastRequest);
        var retry = repeated && _shortGrantStale;
        _lastRequest = desired;
        _requested = true;
        _shortGrantStale = false;
        if ((desired.Equals(Granted) || repeated) && !retry)
            return false;
        var granted = setPos(desired);
        var moved = !granted.Equals(Granted);
        Granted = granted;
        return moved;
    }

    public void NoteExternalChange()
    {
        if (_requested && !Granted.Equals(_lastRequest))
            _shortGrantStale = true;
    }

    public void Clear()
    {
        Granted = default;
        _lastRequest = default;
        _requested = false;
        _shortGrantStale = false;
    }
}
