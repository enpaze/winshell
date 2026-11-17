namespace Winshell.Input;

public sealed class Hotkey : ObservableObject, IHotkeyListener, IDisposable
{
    private const int PollIntervalMs = 16;
    private const int MaxPolls = 3750;

    private readonly IEventLoop _loop;
    private readonly Log _log;
    private readonly HotkeyRegistrar _registrar;
    private string _sequence = "";
    private bool _enabled = true;
    private bool _active;
    private bool _taken;
    private bool _pressed;
    private string _error = "";
    private HotkeySequence? _parsed;
    private IDisposable? _poll;
    private int _polls;
    private bool _disposed;

    internal Hotkey(IEventLoop loop, Log log, HotkeyRegistrar registrar)
    {
        _loop = loop;
        _log = log;
        _registrar = registrar;
    }

    public string Sequence
    {
        get => Read(_sequence);
        set
        {
            if (Set(ref _sequence, value))
                Apply();
        }
    }

    public bool Enabled
    {
        get => Read(_enabled);
        set
        {
            if (Set(ref _enabled, value))
                Apply();
        }
    }

    public bool Active => Read(_active);

    public bool Taken => Read(_taken);

    public bool IsPressed => Read(_pressed);

    public string Error => Read(_error);

    public event EventHandler? Pressed;

    public event EventHandler? Released;

    IEventLoop IHotkeyListener.Loop => _loop;

    void IHotkeyListener.OnPressed(bool deliversRelease)
    {
        if (_disposed)
            return;
        if (Set(ref _pressed, true, nameof(IsPressed)))
            Pressed?.Invoke(this, EventArgs.Empty);
        if (!deliversRelease)
            StartPoll();
    }

    void IHotkeyListener.OnReleased()
    {
        StopPoll();
        if (Set(ref _pressed, false, nameof(IsPressed)))
            Released?.Invoke(this, EventArgs.Empty);
    }

    private void Apply()
    {
        if (_disposed)
            return;
        if (_pressed)
            ((IHotkeyListener)this).OnReleased();
        if (!HotkeySequence.TryParse(_sequence, out var parsed, out var parseError))
        {
            _parsed = null;
            _registrar.Release(this);
            SetStatus(false, false, parseError ?? "");
            return;
        }

        _parsed = parsed;
        if (!_enabled)
        {
            _registrar.Release(this);
            SetStatus(false, false, "");
            return;
        }

        var claim = _registrar.Claim(this, parsed);
        SetStatus(claim.Status == HotkeyClaimStatus.Active, claim.Status == HotkeyClaimStatus.Taken, claim.Error);
    }

    private void SetStatus(bool active, bool taken, string error)
    {
        Set(ref _active, active, nameof(Active));
        Set(ref _taken, taken, nameof(Taken));
        if (Set(ref _error, error, nameof(Error)) && error.Length > 0)
            _log.Warn("hotkey", error);
    }

    private void StartPoll()
    {
        StopPoll();
        _polls = 0;
        _poll = _loop.Schedule(TimeSpan.FromMilliseconds(PollIntervalMs), Tick);
    }

    private void Tick()
    {
        _poll = null;
        _polls++;
        if (!_pressed || _disposed)
            return;
        if (_polls >= MaxPolls || _parsed is null || !IsDown(_parsed.Chords[^1]))
        {
            ((IHotkeyListener)this).OnReleased();
            return;
        }

        _poll = _loop.Schedule(TimeSpan.FromMilliseconds(PollIntervalMs), Tick);
    }

    private void StopPoll()
    {
        _poll?.Dispose();
        _poll = null;
    }

    private bool IsDown(HotkeyChord chord)
    {
        if (chord.IsWinOnly)
            return chord.Side switch
            {
                WinKeySide.Left => Down(VirtualKeys.LeftWin),
                WinKeySide.Right => Down(VirtualKeys.RightWin),
                _ => Down(VirtualKeys.LeftWin) || Down(VirtualKeys.RightWin)
            };
        return Down(chord.VirtualKey) && (VirtualKeys.Held(Down) & chord.Modifiers) == chord.Modifiers;
    }

    private bool Down(int virtualKey)
    {
        return _registrar.IsKeyDown(virtualKey);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        StopPoll();
        _pressed = false;
        _registrar.Release(this);
    }
}
