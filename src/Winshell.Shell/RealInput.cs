using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Winshell.Input;
using Winshell.Native;

namespace Winshell;

/// <summary>
/// The real input behind <c>winshell inspect</c>'s <c>focus</c>, <c>point</c>, <c>leave</c>, <c>press</c>,
/// <c>scroll</c> and <c>key</c>, so <c>PointerEntered</c>, pressed looks and focus rectangles run. It sends nothing
/// while someone uses the PC, no mouse event where another window is on top, and one command at a time. The mouse moves through
/// <c>SendInput</c>, and the cursor goes back afterwards by way of a spot in the same window off the element, so the
/// element sees the pointer leave: putting the cursor back alone raises no <c>PointerExited</c>. Keys and the wheel are
/// posted to the window's input site, which reaches that window only. <c>point</c> leaves the cursor on the element
/// until <c>leave</c>, the next of these commands, <see cref="HoldLimit"/>, or the user moving the mouse.
/// </summary>
internal sealed partial class RealInput(IEventLoop loop) : IDisposable
{
    /// <summary>How long the PC must have gone without the user's input before anything is sent.</summary>
    private static readonly TimeSpan Quiet = TimeSpan.FromSeconds(3);

    /// <summary>How long <c>point</c> keeps the cursor when nothing gives it back.</summary>
    private static readonly TimeSpan HoldLimit = TimeSpan.FromSeconds(30);

    /// <summary>How late Windows may stamp input this sent, after <c>SendInput</c> returned: a few clock ticks.</summary>
    private const uint OwnInputSlack = 50;

    private const int Escape = 0x1B;

    private const int TabLimit = 100;
    private const int Tab = 0x09;
    private const int Shift = 0x10;
    private const int Ctrl = 0x11;
    private const int Alt = 0x12;

    /// <summary>How long a step waits for WinUI to take the input before the next, and what a recording sees of it.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// How long to wait for an element to see the pointer come or go. A window that has just opened can miss the first
    /// move into it, so the move is sent again every <see cref="Retry"/> until then.
    /// </summary>
    private static readonly TimeSpan Arrival = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan Retry = TimeSpan.FromMilliseconds(200);

    private static readonly TimeSpan HoldCheck = TimeSpan.FromMilliseconds(100);

    private Hold? _hold;
    private bool _busy;

    /// <summary>Where the cursor goes back to while a command has it away, and where that command put it.</summary>
    private (POINT Back, POINT At)? _away;

    /// <summary>What became of the cursor when it went back.</summary>
    private enum Given
    {
        /// <summary>It is back, and the element saw the pointer leave.</summary>
        Back,

        /// <summary>It is back, but the element may not have seen the pointer leave.</summary>
        BackStillHovered,

        /// <summary>The user moved it, so it stays where they put it.</summary>
        Taken
    }

    /// <summary>
    /// Whether someone other than this shell used the PC within <see cref="Quiet"/>. <paramref name="lastInput"/> is what
    /// <c>GetLastInputInfo</c> says. When it was stamped during one of this shell's own recent sends,
    /// <paramref name="sent"/>, oldest first, the input that counts is the one that send hid, and so on back. The send
    /// that made a stamp is the first one it falls in: a later send in the same clock tick hid that stamp itself. The
    /// times are <c>GetTickCount</c> milliseconds, which wrap and step about every 16.
    /// </summary>
    internal static bool UserActive(uint now, uint lastInput, IReadOnlyList<Send> sent)
    {
        var stamp = lastInput;
        for (var step = 0; step <= sent.Count; step++)
        {
            var own = sent.Where(send => unchecked((int)(stamp - send.Start)) >= 0 &&
                                         unchecked((int)(stamp - send.End)) <= OwnInputSlack)
                .Select(send => (Send?)send).FirstOrDefault();
            if (own is not { } hid)
                break;
            stamp = hid.Before;
        }

        return unchecked(now - stamp) < Quiet.TotalMilliseconds;
    }

    /// <summary>Moves the cursor onto the element and leaves it there.</summary>
    public async Task<string> Point(FrameworkElement element, string name)
    {
        using var turn = Turn();
        await Release();
        var target = Aim(element, name);
        RefuseWhileUsed();
        var back = CursorNow();
        var landed = await Arrive(target, back);
        var hold = new Hold(target, back, landed, loop.Time.GetTimestamp());
        _hold = hold;
        _away = null;
        hold.Timer = loop.Time.CreateTimer(_ => loop.Post(() => Check(hold)), null, HoldCheck, HoldCheck);
        return $"Pointing at {name}. winshell inspect leave puts the cursor back.";
    }

    public async Task<string> Leave()
    {
        using var turn = Turn();
        var hold = _hold;
        return await Release() switch
        {
            null => "Nothing is pointed at: there was no point, or it has ended.",
            Given.Back => "The cursor is back where it was.",
            Given.BackStillHovered =>
                $"The cursor is back where it was, but {hold!.Target.Name} may keep its hover look until the mouse moves.",
            _ => "The mouse moved since point, so the cursor stays where it is."
        };
    }

    /// <summary>A left or right button's press and release on the element.</summary>
    public async Task<string> Press(FrameworkElement element, string name, bool right)
    {
        using var turn = Turn();
        await Release();
        var target = Aim(element, name);
        RefuseWhileUsed();
        var back = CursorNow();
        var landed = await Arrive(target, back);
        Given given;
        try
        {
            RefuseAt(target, landed);
            if (!IsAt(landed))
                throw new IpcException("The mouse moved, so nothing more was sent.");
            // The press carries its own move to the checked point, so it can't land where the cursor went since.
            Sent(PointerInput.Press(landed, right));
            await Task.Delay(Settle, loop.Time);
        }
        finally
        {
            given = await GiveBack(target, back, landed);
        }

        return (right ? $"Right-pressed {name}" : $"Pressed {name}") + Afterwards(given);
    }

    /// <summary>
    /// Turns the wheel over the element: a positive number of steps scrolls down. The mouse comes onto the element as for
    /// a press, but the notches are posted to its window: the wheel <c>SendInput</c> sends doesn't reach a WinUI window,
    /// and a posted one does once the window has seen the mouse come.
    /// </summary>
    public async Task<string> Scroll(FrameworkElement element, string name, int steps)
    {
        using var turn = Turn();
        await Release();
        var target = Aim(element, name);
        var site = Site(target.Window);
        RefuseWhileUsed();
        var back = CursorNow();
        var landed = await Arrive(target, back);
        Given given;
        try
        {
            for (var notch = 0; notch < Math.Abs(steps); notch++)
            {
                using var turned = new Sighting(element, UIElement.PointerWheelChangedEvent);
                RefuseAt(target, landed);
                if (!InputSite.PostWheel(site, landed, steps > 0))
                    throw new IpcException("Windows took none or only part of the input.");
                await Task.WhenAny(turned.Seen, Task.Delay(Arrival, loop.Time));
                if (!turned.Seen.IsCompleted)
                    throw new IpcException(
                        $"{name} didn't see the wheel turn within {Arrival.TotalSeconds:0} seconds, so nothing more was sent.");
            }

            await Task.Delay(Settle, loop.Time);
        }
        finally
        {
            given = await GiveBack(target, back, landed);
        }

        return $"Scrolled {name} {(steps > 0 ? "down" : "up")} {Math.Abs(steps)}" + Afterwards(given);
    }

    /// <summary>Presses Tab in the element's window until keyboard focus is on the element or in it.</summary>
    public async Task<string> Focus(FrameworkElement element, string name)
    {
        using var turn = Turn();
        await Release();
        var site = Site(WindowOf(element));
        var root = element.XamlRoot ?? throw new IpcException($"{name} is not shown.");
        RefuseWhileUsed();
        object? first = null;
        for (var tabs = 0; tabs <= TabLimit; tabs++)
        {
            var focused = FocusManager.GetFocusedElement(root);
            if (focused is UIElement { FocusState: FocusState.Keyboard } shown && Holds(element, shown))
                return tabs == 1 ? $"Focused {name} with 1 Tab" : $"Focused {name} with {tabs} Tabs";
            // Back at the first element Tab reached: it went all the way round.
            if (tabs > 1 && ReferenceEquals(focused, first))
                break;
            if (tabs == 1)
                first = focused;
            if (!await Tabbed(site))
                break;
        }

        throw new IpcException(
            $"Tab went round without reaching {name}. Keyboard focus reaches an enabled control with IsTabStop, such as a button, and not a panel or text.");
    }

    /// <summary>
    /// Each chord in turn. A plain key is posted to the window, in front or not. A chord with Ctrl, Alt or Shift goes
    /// through the real keyboard, which only the window in front receives, so it needs the window to be in front. A
    /// chord that Windows or another program takes before the window in front, such as Alt+Tab or a registered
    /// hotkey, is refused.
    /// </summary>
    public async Task<string> Key(ShellWindow shown, string keys)
    {
        using var turn = Turn();
        await Release();
        if (!HotkeySequence.TryParse(keys, out var sequence, out var error))
            throw new IpcException(error ?? "No keys were given.");
        if (sequence!.Chords.FirstOrDefault(chord => (chord.Modifiers & HotkeyModifiers.Win) != 0) is { } win)
            throw new IpcException($"{win.Text} holds the Windows key, which Windows itself takes. Nothing was sent.");
        if (sequence.Chords.FirstOrDefault(Taken) is { } taken)
            throw new IpcException(
                $"{taken.Text} is taken before the window in front gets it, by Windows or by a program that has it as a hotkey. Nothing was sent.");
        var window = shown.Host?.Handle is { } handle and not 0
            ? handle
            : throw new IpcException("That window has no native window.");
        var site = Site(window);
        RefuseWhileUsed();
        foreach (var chord in sequence.Chords)
        {
            if (chord.Modifiers == HotkeyModifiers.None)
            {
                if (!InputSite.PostKey(site, chord.VirtualKey))
                    throw new IpcException("Windows took none or only part of the input.");
            }
            else
            {
                if (!Ours(TopLevelWindows.RootOf(Win32.GetForegroundWindow()), window))
                    throw new IpcException(
                        $"{chord.Text} holds a modifier, which only the real keyboard carries, and Windows gives that to the window in front. This window isn't in front, so nothing more was sent. A plain key, such as Tab or Enter, reaches any window.");
                RefuseWhileUsed();
                Sent(KeyInput.Chord([.. Modifiers(chord.Modifiers), chord.VirtualKey]));
            }

            await Task.Delay(Settle, loop.Time);
        }

        return $"Pressed {sequence.Text}";
    }

    /// <summary>
    /// A cursor a command or a <c>point</c> has away goes back as the shell ends, with no time to step off the element
    /// first.
    /// </summary>
    public void Dispose()
    {
        var hold = _hold;
        _hold = null;
        hold?.Timer?.Dispose();
        var away = _away ?? (hold is null ? null : (hold.Back, hold.At));
        if (away is var (back, at) && IsAt(at))
            PutBack(back);
        _away = null;
    }

    /// <summary>
    /// A chord that doesn't reach the window in front: Windows' own, such as Ctrl+Esc, which opens Start, Alt+Tab and
    /// Ctrl+Shift+Esc, or a hotkey a program registered, this one included.
    /// </summary>
    private static bool Taken(HotkeyChord chord)
    {
        var alt = (chord.Modifiers & HotkeyModifiers.Alt) != 0;
        var ctrl = (chord.Modifiers & HotkeyModifiers.Ctrl) != 0;
        if ((chord.VirtualKey == Escape && (alt || ctrl)) || (chord.VirtualKey == Tab && alt))
            return true;
        return chord.Modifiers != HotkeyModifiers.None &&
               !KeyInput.Unclaimed((uint)chord.Modifiers, chord.VirtualKey);
    }

    /// <summary>What a command adds when the cursor didn't simply go back.</summary>
    private static string Afterwards(Given given)
    {
        return given switch
        {
            Given.Taken => ". The mouse moved, so the cursor stays where it is.",
            Given.BackStillHovered => ". It may keep its hover look until the mouse moves.",
            _ => ""
        };
    }

    /// <summary>One real input command at a time: two at once would each move the cursor, and lose where it was.</summary>
    private Turned Turn()
    {
        if (_busy)
            throw new IpcException("Another real input command is still running. Nothing was sent.");
        _busy = true;
        return new Turned(this);
    }

    private static IEnumerable<int> Modifiers(HotkeyModifiers modifiers)
    {
        if ((modifiers & HotkeyModifiers.Ctrl) != 0)
            yield return Ctrl;
        if ((modifiers & HotkeyModifiers.Alt) != 0)
            yield return Alt;
        if ((modifiers & HotkeyModifiers.Shift) != 0)
            yield return Shift;
    }

    private static bool Holds(FrameworkElement element, DependencyObject? inside)
    {
        for (; inside is not null; inside = VisualTreeHelper.GetParent(inside))
            if (ReferenceEquals(inside, element))
                return true;
        return false;
    }

    private static nint WindowOf(FrameworkElement element)
    {
        return ShellWindow.Of(element)?.Host?.Handle is { } handle and not 0
            ? handle
            : throw new IpcException("That element's window has no native window.");
    }

    private static nint Site(nint window)
    {
        return InputSite.Of(window) is var site and not 0
            ? site
            : throw new IpcException("That window has no input site to take keys and the wheel.");
    }

    private static POINT CursorNow()
    {
        return PointerInput.Cursor() ?? throw new IpcException("Windows won't say where the cursor is.");
    }

    /// <summary>Whether the cursor is where this left it. A move <c>SendInput</c> scales can land a pixel to the side.</summary>
    private static bool IsAt(POINT point)
    {
        return PointerInput.Cursor() is { } now && Near(now, point);
    }

    private static bool Near(POINT one, POINT other)
    {
        return Math.Abs(one.X - other.X) <= 1 && Math.Abs(one.Y - other.Y) <= 1;
    }

    /// <summary>A window of the target's: its own, or a flyout's or menu's, which is owned by the window it opened from.</summary>
    private static bool Ours(nint root, nint window)
    {
        return root == window || Win32.GetWindow(root, Win32.GW_OWNER) == window;
    }

    private static POINT? OnScreen(nint window, XamlRoot root, Point at)
    {
        var point = new POINT
        {
            X = (int)Math.Round(at.X * root.RasterizationScale),
            Y = (int)Math.Round(at.Y * root.RasterizationScale)
        };
        return Win32.ClientToScreen(window, ref point) ? point : null;
    }

    /// <summary>
    /// The element's window, and the middle of the element on the screen in physical pixels. The element must be what
    /// XAML finds at that point: one clipped away, covered by another element, or not hit-testable takes no pointer.
    /// </summary>
    private static Target Aim(FrameworkElement element, string name)
    {
        var window = WindowOf(element);
        if (element.XamlRoot is not { } root || element.ActualWidth < 1 || element.ActualHeight < 1)
            throw new IpcException($"{name} has no size on screen, so nothing was sent.");
        var middle = element.TransformToVisual(null)
            .TransformPoint(new Point(element.ActualWidth / 2, element.ActualHeight / 2));
        DependencyObject top = element;
        while (VisualTreeHelper.GetParent(top) is { } parent)
            top = parent;
        var hit = VisualTreeHelper.FindElementsInHostCoordinates(middle, (UIElement)top).FirstOrDefault();
        if (!Holds(element, hit))
            throw new IpcException(
                $"{name} isn't what shows at its middle: it is clipped, covered or not hit-testable. Nothing was sent.");
        var at = OnScreen(window, root, middle) ?? throw new IpcException("Windows won't say where that window is.");
        return new Target(element, name, window, at);
    }

    /// <summary>
    /// Moves the real mouse onto the element until the element sees it come, and says where the cursor landed. A move
    /// that goes unseen, as the first into a window that has just opened can, is sent again a pixel to the side. When
    /// the element never sees it, or a check fails between moves, the cursor goes <paramref name="back"/>.
    /// </summary>
    private async Task<POINT> Arrive(Target target, POINT back)
    {
        var at = target.At;
        try
        {
            using (var come = new Sighting(target.Element, UIElement.PointerEnteredEvent, UIElement.PointerMovedEvent))
            {
                for (var attempt = 0; attempt * Retry < Arrival && !come.Seen.IsCompleted; attempt++)
                {
                    var next = target.At with { X = target.At.X + attempt % 2 };
                    RefuseAt(target, next);
                    _away = (back, next);
                    Sent(PointerInput.MoveTo(next));
                    at = next;
                    await Task.WhenAny(come.Seen, Task.Delay(Retry, loop.Time));
                }

                if (!come.Seen.IsCompleted)
                    throw new IpcException(
                        $"{target.Name} didn't see the pointer come within {Arrival.TotalSeconds:0} seconds, so nothing more was sent.");
            }

            var landed = CursorNow();
            if (!Near(landed, at))
                throw new IpcException(
                    $"The cursor went to ({landed.X}, {landed.Y}), not ({at.X}, {at.Y}). Something else moved it, so nothing more was sent.");
            _away = (back, landed);
            return landed;
        }
        catch (IpcException)
        {
            if (_away is not null && IsAt(at))
                PutBack(back);
            _away = null;
            throw;
        }
    }

    /// <summary>Puts the cursor at <paramref name="back"/>, and keeps the time, so the move isn't taken for the user's.</summary>
    private static void PutBack(POINT back)
    {
        SentInput.Around(() => Cursor.MoveTo(back.X, back.Y));
    }

    /// <summary>Posts Tab and waits up to a second for keyboard focus to move. False when it didn't.</summary>
    private async Task<bool> Tabbed(nint site)
    {
        var moved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<FocusManagerGotFocusEventArgs> got = (_, _) => moved.TrySetResult();
        FocusManager.GotFocus += got;
        try
        {
            if (!InputSite.PostKey(site, Tab))
                throw new IpcException("Windows took none or only part of the input.");
            await moved.Task.WaitAsync(TimeSpan.FromSeconds(1), loop.Time);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            FocusManager.GotFocus -= got;
        }
    }

    /// <summary>What every pointer event checks first: nobody is using the PC, and the window is on top at the point.</summary>
    private static void RefuseAt(Target target, POINT at)
    {
        RefuseWhileUsed();
        if (!Ours(PointerInput.RootWindowAt(at), target.Window))
            throw new IpcException(
                $"Another window is over {target.Name} at ({at.X}, {at.Y}), and real input there would reach it. Nothing was sent.");
    }

    /// <summary>Refuses while someone uses the PC, or when Windows won't say whether anyone does.</summary>
    private static void RefuseWhileUsed()
    {
        if (SentInput.LastInput() is not { } last)
            throw new IpcException("Windows won't say when the PC was last used, so nothing was sent.");
        if (UserActive(Win32.GetTickCount(), last, SentInput.Recent))
            throw new IpcException(
                $"Someone used the mouse or keyboard in the last {Quiet.TotalSeconds:0} seconds, and real input would get in their way. Nothing was sent; try again once the PC is left alone.");
    }

    private static void Sent(bool all)
    {
        if (!all)
            throw new IpcException("Windows took none or only part of the input.");
    }

    /// <summary>
    /// Gives the cursor back, unless the user has taken it: it is no longer where this left it. It first moves to a spot
    /// in the same window off the element, so the element sees the pointer leave.
    /// </summary>
    private async Task<Given> GiveBack(Target target, POINT back, POINT left)
    {
        if (!IsAt(left))
        {
            _away = null;
            return Given.Taken;
        }

        var stepped = false;
        var now = left;
        if (Off(target) is { } off)
        {
            using var go = new Sighting(target.Element, UIElement.PointerExitedEvent);
            try
            {
                RefuseAt(target, off);
                _away = (back, off);
                Sent(PointerInput.MoveTo(off));
                now = off;
                await Task.WhenAny(go.Seen, Task.Delay(Arrival, loop.Time));
                stepped = go.Seen.IsCompleted;
            }
            catch (IpcException)
            {
                // The user came back, or a window came over the spot. The cursor still goes back, and nothing else moves.
            }
        }

        _away = null;
        if (!IsAt(now))
            return Given.Taken;
        PutBack(back);
        return stepped ? Given.Back : Given.BackStillHovered;
    }

    /// <summary>
    /// A spot in the target's window, on top there, where XAML finds something other than the element. Best is one
    /// where it finds nothing, or only what holds the element, which has the pointer already: there no other control
    /// gets a hover it would keep.
    /// </summary>
    private static POINT? Off(Target target)
    {
        if (target.Element.XamlRoot is not { Content: { } content } root)
            return null;
        var (width, height) = (root.Size.Width, root.Size.Height);
        POINT? other = null;
        for (var row = 0; row < 5; row++)
            for (var column = 0; column < 5; column++)
            {
                var at = new Point((column + 0.5) * width / 5, (row + 0.5) * height / 5);
                var hit = VisualTreeHelper.FindElementsInHostCoordinates(at, content).FirstOrDefault();
                if (Holds(target.Element, hit) ||
                    OnScreen(target.Window, root, at) is not { } point ||
                    !Ours(PointerInput.RootWindowAt(point), target.Window))
                    continue;
                if (hit is not FrameworkElement framed || Holds(framed, target.Element))
                    return point;
                other ??= point;
            }

        return other;
    }

    /// <summary>Ends a <c>point</c>, and says what became of the cursor, or null when nothing was held.</summary>
    private async Task<Given?> Release()
    {
        if (_hold is not { } hold)
            return null;
        _hold = null;
        hold.Timer?.Dispose();
        return await GiveBack(hold.Target, hold.Back, hold.At);
    }

    private async Task Expire()
    {
        using var turn = Turn();
        await Release();
    }

    /// <summary>A held cursor goes back after <see cref="HoldLimit"/>, and is let go at once when the user moves it.</summary>
    private void Check(Hold hold)
    {
        // A command that is running ends the hold itself.
        if (!ReferenceEquals(_hold, hold) || _busy)
            return;
        if (!IsAt(hold.At))
        {
            _hold = null;
            hold.Timer?.Dispose();
            return;
        }

        if (loop.Time.GetElapsedTime(hold.Since) >= HoldLimit)
            _ = Expire();
    }

    /// <summary>An element to aim at, its window, and its middle on the screen.</summary>
    private sealed record Target(FrameworkElement Element, string Name, nint Window, POINT At);

    private sealed record Hold(Target Target, POINT Back, POINT At, long Since)
    {
        public ITimer? Timer { get; set; }
    }

    private sealed partial class Turned(RealInput input) : IDisposable
    {
        public void Dispose()
        {
            input._busy = false;
        }
    }

    /// <summary>Whether an element raised one of some pointer events, handled or not, while this is undisposed.</summary>
    private sealed partial class Sighting : IDisposable
    {
        private readonly UIElement _element;
        private readonly RoutedEvent[] _events;
        private readonly PointerEventHandler _handler;
        private readonly TaskCompletionSource _seen = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Sighting(UIElement element, params RoutedEvent[] events)
        {
            _element = element;
            _events = events;
            _handler = (_, _) => _seen.TrySetResult();
            foreach (var routed in events)
                element.AddHandler(routed, _handler, true);
        }

        public Task Seen => _seen.Task;

        public void Dispose()
        {
            foreach (var routed in _events)
                _element.RemoveHandler(routed, _handler);
        }
    }
}
