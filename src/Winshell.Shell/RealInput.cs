using Windows.Foundation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Winshell.Input;
using Winshell.Native;

namespace Winshell;

/// <summary>
/// The real input behind <c>winshell inspect</c>'s <c>focus</c>, <c>point</c>, <c>leave</c>, <c>press</c>,
/// <c>scroll</c> and <c>key</c>, so <c>PointerEntered</c>, pressed looks and focus rectangles run. It sends nothing
/// while someone uses the PC, and no pointer event where another window is on top. The mouse moves through
/// <c>SendInput</c>, and the cursor goes back afterwards by way of a spot in the same window off the element, so the
/// element sees the pointer leave: putting the cursor back alone raises no <c>PointerExited</c>. Keys and the wheel are
/// posted to the window's input site, which reaches that window only. <c>point</c> leaves the cursor on the element
/// until <c>leave</c>, the next of these commands, <see cref="HoldLimit"/>, or the user moving the mouse.
/// </summary>
internal sealed class RealInput(IEventLoop loop) : IDisposable
{
    /// <summary>How long the PC must have gone without the user's input before anything is sent.</summary>
    internal static readonly TimeSpan Quiet = TimeSpan.FromSeconds(3);

    /// <summary>How long <c>point</c> keeps the cursor when nothing gives it back.</summary>
    internal static readonly TimeSpan HoldLimit = TimeSpan.FromSeconds(30);

    /// <summary>How late Windows may stamp input this sent, after <c>SendInput</c> returned.</summary>
    private const uint OwnInputSlack = 250;

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

    /// <summary>
    /// Whether someone other than this shell used the PC within <see cref="Quiet"/>: <paramref name="lastInput"/> is
    /// what <c>GetLastInputInfo</c> says, and input stamped by the time this shell last sent its own,
    /// <paramref name="sent"/>, is that input. The times are <c>GetTickCount</c> milliseconds, which wrap.
    /// </summary>
    internal static bool UserActive(uint now, uint lastInput, uint? sent)
    {
        if (unchecked(now - lastInput) >= Quiet.TotalMilliseconds)
            return false;
        return sent is not { } own || unchecked((int)(lastInput - own)) > OwnInputSlack;
    }

    /// <summary>Moves the cursor onto the element and leaves it there.</summary>
    public async Task<string> Point(FrameworkElement element, string name)
    {
        await Release();
        var target = Aim(element, name);
        RefuseWhileUsed();
        var back = CursorNow();
        var landed = await Arrive(target, back);
        var hold = new Hold(target, back, landed, loop.Time.GetTimestamp());
        _hold = hold;
        hold.Timer = loop.Time.CreateTimer(_ => loop.Post(() => Check(hold)), null, HoldCheck, HoldCheck);
        return $"Pointing at {name}. winshell inspect leave puts the cursor back.";
    }

    public async Task<string> Leave()
    {
        var hold = _hold;
        if (!await Release())
            return "Nothing was pointed at.";
        return hold!.SteppedOff
            ? "The cursor is back where it was."
            : $"The cursor is back where it was. {hold.Target.Name} fills its window, so it may keep its hover look until the mouse moves.";
    }

    /// <summary>A left or right button's press and release on the element.</summary>
    public async Task<string> Press(FrameworkElement element, string name, bool right)
    {
        await Release();
        var target = Aim(element, name);
        RefuseWhileUsed();
        var back = CursorNow();
        var landed = await Arrive(target, back);
        try
        {
            RefuseAt(target, landed);
            Sent(PointerInput.Press(right));
            await Task.Delay(Settle, loop.Time);
        }
        finally
        {
            await GiveBack(target, back, landed);
        }

        return right ? $"Right-pressed {name}" : $"Pressed {name}";
    }

    /// <summary>
    /// Turns the wheel over the element: a positive number of steps scrolls down. The mouse comes onto the element as for
    /// a press, but the notches are posted to its window: the wheel <c>SendInput</c> sends doesn't reach a WinUI window,
    /// and a posted one does once the window has seen the mouse come.
    /// </summary>
    public async Task<string> Scroll(FrameworkElement element, string name, int steps)
    {
        await Release();
        var target = Aim(element, name);
        var site = Site(target.Window);
        RefuseWhileUsed();
        var back = CursorNow();
        var landed = await Arrive(target, back);
        try
        {
            // Each notch is sent again until the element sees it, so one that goes missing isn't lost.
            for (var notch = 0; notch < Math.Abs(steps); notch++)
            {
                using var turned = new Sighting(element, UIElement.PointerWheelChangedEvent);
                for (var attempt = 0; !turned.Seen.IsCompleted; attempt++)
                {
                    if (attempt * Retry >= Arrival)
                        throw new IpcException(
                            $"{name} didn't see the wheel turn within {Arrival.TotalSeconds:0} seconds, so nothing more was sent.");
                    RefuseAt(target, landed);
                    if (!InputSite.PostWheel(site, landed, steps > 0))
                        throw new IpcException("Windows took none or only part of the input.");
                    await Task.WhenAny(turned.Seen, Task.Delay(Retry, loop.Time));
                }
            }

            await Task.Delay(Settle, loop.Time);
        }
        finally
        {
            await GiveBack(target, back, landed);
        }

        return $"Scrolled {name} {(steps > 0 ? "down" : "up")} {Math.Abs(steps)}";
    }

    /// <summary>Presses Tab in the element's window until keyboard focus is on the element or in it.</summary>
    public async Task<string> Focus(FrameworkElement element, string name)
    {
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
    /// through the real keyboard, which only the window in front receives, so it needs the window to be in front.
    /// </summary>
    public async Task<string> Key(ShellWindow shown, string keys)
    {
        await Release();
        if (!HotkeySequence.TryParse(keys, out var sequence, out var error))
            throw new IpcException(error ?? "No keys were given.");
        if (sequence!.Chords.FirstOrDefault(chord => (chord.Modifiers & HotkeyModifiers.Win) != 0) is { } win)
            throw new IpcException($"{win.Text} holds the Windows key, which Windows itself takes. Nothing was sent.");
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
                if (TopLevelWindows.RootOf(Win32.GetForegroundWindow()) != window)
                    throw new IpcException(
                        $"{chord.Text} holds a modifier, which only the real keyboard carries, and Windows gives that to the window in front. This window isn't in front, so nothing more was sent. A plain key, such as Tab or Enter, reaches any window.");
                RefuseWhileUsed();
                Sent(KeyInput.Chord([.. Modifiers(chord.Modifiers), chord.VirtualKey]));
            }

            await Task.Delay(Settle, loop.Time);
        }

        return $"Pressed {sequence.Text}";
    }

    /// <summary>A held cursor goes back as the shell ends, with no time to step off the element first.</summary>
    public void Dispose()
    {
        if (_hold is not { } hold)
            return;
        _hold = null;
        hold.Timer?.Dispose();
        if (IsAt(hold.At))
            Cursor.MoveTo(hold.Back.X, hold.Back.Y);
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

    private static bool IsAt(POINT point)
    {
        return PointerInput.Cursor() is { } now && now.X == point.X && now.Y == point.Y;
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
            X = (int)Math.Round(at.X * root.RasterizationScale), Y = (int)Math.Round(at.Y * root.RasterizationScale)
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
                    at = target.At with { X = target.At.X + attempt % 2 };
                    RefuseAt(target, at);
                    Sent(PointerInput.MoveTo(at));
                    await Task.WhenAny(come.Seen, Task.Delay(Retry, loop.Time));
                }

                if (!come.Seen.IsCompleted)
                    throw new IpcException(
                        $"{target.Name} didn't see the pointer come within {Arrival.TotalSeconds:0} seconds, so nothing more was sent.");
            }

            var landed = CursorNow();
            if (Math.Abs(landed.X - at.X) > 1 || Math.Abs(landed.Y - at.Y) > 1)
                throw new IpcException(
                    $"The cursor went to ({landed.X}, {landed.Y}), not ({at.X}, {at.Y}). Something else moved it, so nothing more was sent.");
            return landed;
        }
        catch (IpcException)
        {
            if (IsAt(at))
                Cursor.MoveTo(back.X, back.Y);
            throw;
        }
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

    private static void RefuseWhileUsed()
    {
        var info = new LASTINPUTINFO { cbSize = 8 };
        if (Win32.GetLastInputInfo(ref info) && UserActive(Win32.GetTickCount(), info.dwTime, SentInput.Last))
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
    /// in the same window off the element, so the element sees the pointer leave. True when the element saw that.
    /// </summary>
    private async Task<bool> GiveBack(Target target, POINT back, POINT left)
    {
        if (!IsAt(left))
            return false;
        var stepped = false;
        var now = left;
        if (Off(target) is { } off)
        {
            using var go = new Sighting(target.Element, UIElement.PointerExitedEvent);
            try
            {
                RefuseAt(target, off);
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

        if (Math.Abs(CursorNow().X - now.X) <= 1 && Math.Abs(CursorNow().Y - now.Y) <= 1)
            Cursor.MoveTo(back.X, back.Y);
        return stepped;
    }

    /// <summary>A spot in the target's window, on top there, where XAML finds something other than the element.</summary>
    private static POINT? Off(Target target)
    {
        if (target.Element.XamlRoot is not { Content: { } content } root)
            return null;
        var (width, height) = (root.Size.Width, root.Size.Height);
        for (var row = 0; row < 5; row++)
        for (var column = 0; column < 5; column++)
        {
            var at = new Point((column + 0.5) * width / 5, (row + 0.5) * height / 5);
            if (Holds(target.Element, VisualTreeHelper.FindElementsInHostCoordinates(at, content).FirstOrDefault()))
                continue;
            if (OnScreen(target.Window, root, at) is { } point && Ours(PointerInput.RootWindowAt(point), target.Window))
                return point;
        }

        return null;
    }

    /// <summary>Ends a <c>point</c>. True when one was held.</summary>
    private async Task<bool> Release()
    {
        if (_hold is not { } hold)
            return false;
        _hold = null;
        hold.Timer?.Dispose();
        hold.SteppedOff = await GiveBack(hold.Target, hold.Back, hold.At);
        return true;
    }

    /// <summary>A held cursor goes back after <see cref="HoldLimit"/>, and is let go at once when the user moves it.</summary>
    private void Check(Hold hold)
    {
        if (!ReferenceEquals(_hold, hold))
            return;
        if (!IsAt(hold.At))
        {
            _hold = null;
            hold.Timer?.Dispose();
            return;
        }

        if (loop.Time.GetElapsedTime(hold.Since) >= HoldLimit)
            _ = Release();
    }

    /// <summary>An element to aim at, its window, and its middle on the screen.</summary>
    private sealed record Target(FrameworkElement Element, string Name, nint Window, POINT At);

    private sealed record Hold(Target Target, POINT Back, POINT At, long Since)
    {
        public ITimer? Timer { get; set; }

        public bool SteppedOff { get; set; }
    }

    /// <summary>Whether an element raised one of some pointer events, handled or not, while this is undisposed.</summary>
    private sealed class Sighting : IDisposable
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
