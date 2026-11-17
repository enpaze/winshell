namespace Winshell.Input;

internal readonly record struct ArmNotice(IHotkeyListener Listener, long Token, long DeadlineMs);

internal sealed class BookResult
{
    public List<IHotkeyListener> Pressed { get; } = [];

    public List<ArmNotice> Armed { get; } = [];

    public bool Swallow { get; set; }

    public bool MaskWin { get; set; }
}

internal sealed class HotkeyBook
{
    private const long TimeoutMs = 1000;

    private readonly Dictionary<OsChord, List<IHotkeyListener>> _os = [];
    private readonly Dictionary<WinKeySide, List<IHotkeyListener>> _win = [];
    private readonly Dictionary<IHotkeyListener, HotkeySequence> _sequences = [];
    private readonly List<Armed> _armed = [];
    private long _token;
    private bool _leftDown;
    private bool _rightDown;
    private bool _leftUsed;
    private bool _rightUsed;

    public bool NeedsHook => _win.Count > 0 || _armed.Count > 0;

    public bool HasOs(OsChord chord)
    {
        return _os.ContainsKey(chord);
    }

    public void Add(IHotkeyListener listener, HotkeySequence sequence)
    {
        _sequences.Add(listener, sequence);
        var first = sequence.Chords[0];
        if (first.IsWinOnly)
            AddTo(_win, first.Side, listener);
        else
            AddTo(_os, first.OsKey, listener);
    }

    public OsChord? Remove(IHotkeyListener listener)
    {
        _armed.RemoveAll(armed => armed.Listener == listener);
        if (!_sequences.Remove(listener, out var sequence))
            return null;
        var first = sequence.Chords[0];
        if (first.IsWinOnly)
        {
            RemoveFrom(_win, first.Side, listener);
            return null;
        }

        return RemoveFrom(_os, first.OsKey, listener) ? first.OsKey : null;
    }

    public void Clear()
    {
        _os.Clear();
        _win.Clear();
        _sequences.Clear();
        _armed.Clear();
        _leftDown = _rightDown = _leftUsed = _rightUsed = false;
    }

    public BookResult Trigger(OsChord chord, long nowMs)
    {
        var result = new BookResult();
        Expire(nowMs);
        if (_os.TryGetValue(chord, out var list))
            Fire([.. list], nowMs, result, null);
        return result;
    }

    public void Disarm(IHotkeyListener listener, long token)
    {
        _armed.RemoveAll(armed => armed.Listener == listener && armed.Token == token);
    }

    public BookResult Hook(int virtualKey, bool isUp, HotkeyModifiers modifiers, long nowMs)
    {
        var result = new BookResult();
        Expire(nowMs);
        if (virtualKey is VirtualKeys.LeftWin or VirtualKeys.RightWin)
        {
            HandleWin(virtualKey == VirtualKeys.LeftWin ? WinKeySide.Left : WinKeySide.Right, isUp, modifiers, nowMs,
                result);
            return result;
        }

        if (isUp || VirtualKeys.IsModifier(virtualKey))
        {
            if (!isUp)
                MarkWinUsed();
            return result;
        }

        MarkWinUsed();
        var matched = new List<Armed>();
        foreach (var armed in _armed)
        {
            var expect = armed.Sequence.Chords[armed.Next];
            if (!expect.IsWinOnly && expect.VirtualKey == virtualKey && expect.Modifiers == modifiers)
                matched.Add(armed);
        }

        _armed.Clear();
        if (matched.Count == 0)
            return result;
        foreach (var armed in matched)
            Continue(armed, nowMs, result);
        result.Swallow = true;
        return result;
    }

    private void HandleWin(WinKeySide side, bool isUp, HotkeyModifiers modifiers, long nowMs, BookResult result)
    {
        if (!isUp)
        {
            if (side == WinKeySide.Left)
            {
                if (_leftDown)
                    return;
                _leftDown = true;
                _leftUsed = false;
            }
            else
            {
                if (_rightDown)
                    return;
                _rightDown = true;
                _rightUsed = false;
            }

            return;
        }

        var used = side == WinKeySide.Left ? _leftUsed : _rightUsed;
        if (side == WinKeySide.Left)
            _leftDown = false;
        else
            _rightDown = false;
        if (used || (modifiers & ~HotkeyModifiers.Win) != 0)
            return;
        var skip = AdvanceWin(side, nowMs, result);
        FireWin(side, nowMs, result, skip);
        FireWin(WinKeySide.Either, nowMs, result, skip);
        var handled = result.Pressed.Count > 0 || result.Armed.Count > 0;
        result.Swallow = handled;
        result.MaskWin = handled;
    }

    private HashSet<IHotkeyListener> AdvanceWin(WinKeySide side, long nowMs, BookResult result)
    {
        var matched = new List<Armed>();
        foreach (var armed in _armed)
        {
            var expect = armed.Sequence.Chords[armed.Next];
            if (expect.IsWinOnly && (expect.Side == WinKeySide.Either || expect.Side == side))
                matched.Add(armed);
        }

        _armed.Clear();
        var skip = new HashSet<IHotkeyListener>();
        foreach (var armed in matched)
        {
            skip.Add(armed.Listener);
            Continue(armed, nowMs, result);
        }

        return skip;
    }

    private void FireWin(WinKeySide side, long nowMs, BookResult result, HashSet<IHotkeyListener> skip)
    {
        if (_win.TryGetValue(side, out var list))
            Fire([.. list], nowMs, result, skip);
    }

    private void Fire(IReadOnlyList<IHotkeyListener> listeners, long nowMs, BookResult result,
        HashSet<IHotkeyListener>? skip)
    {
        foreach (var listener in listeners)
        {
            if (skip is not null && skip.Contains(listener))
                continue;
            if (!_sequences.TryGetValue(listener, out var sequence))
                continue;
            _armed.RemoveAll(armed => armed.Listener == listener);
            if (sequence.Chords.Count <= 1)
                result.Pressed.Add(listener);
            else
                StartArm(listener, sequence, nowMs, result);
        }
    }

    private void StartArm(IHotkeyListener listener, HotkeySequence sequence, long nowMs, BookResult result)
    {
        var armed = new Armed
        {
            Listener = listener,
            Sequence = sequence,
            Next = 1,
            Token = ++_token,
            Deadline = nowMs + TimeoutMs
        };
        _armed.Add(armed);
        result.Armed.Add(new ArmNotice(listener, armed.Token, armed.Deadline));
    }

    private void Continue(Armed armed, long nowMs, BookResult result)
    {
        var next = armed.Next + 1;
        if (next >= armed.Sequence.Chords.Count)
        {
            result.Pressed.Add(armed.Listener);
            return;
        }

        armed.Next = next;
        armed.Token = ++_token;
        armed.Deadline = nowMs + TimeoutMs;
        _armed.Add(armed);
        result.Armed.Add(new ArmNotice(armed.Listener, armed.Token, armed.Deadline));
    }

    private void MarkWinUsed()
    {
        if (_leftDown)
            _leftUsed = true;
        if (_rightDown)
            _rightUsed = true;
    }

    private void Expire(long nowMs)
    {
        _armed.RemoveAll(armed => nowMs >= armed.Deadline);
    }

    private static void AddTo<TKey>(Dictionary<TKey, List<IHotkeyListener>> map, TKey key, IHotkeyListener listener)
        where TKey : notnull
    {
        if (!map.TryGetValue(key, out var list))
            map[key] = list = [];
        list.Add(listener);
    }

    private static bool RemoveFrom<TKey>(Dictionary<TKey, List<IHotkeyListener>> map, TKey key,
        IHotkeyListener listener)
        where TKey : notnull
    {
        if (!map.TryGetValue(key, out var list) || !list.Remove(listener) || list.Count > 0)
            return false;
        map.Remove(key);
        return true;
    }

    private sealed class Armed
    {
        public required IHotkeyListener Listener { get; init; }

        public required HotkeySequence Sequence { get; init; }

        public int Next { get; set; }

        public long Token { get; set; }

        public long Deadline { get; set; }
    }
}
