using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Winshell.Input;
using Winshell.Native;

namespace Winshell;

internal sealed partial class RealInput(IEventLoop loop) : IDisposable
{
    private static readonly TimeSpan HoldLimit = TimeSpan.FromSeconds(30);

    private const int Escape = 0x1B;

    private const int TabLimit = 100;
    private const int Tab = 0x09;
    private const int Shift = 0x10;
    private const int Ctrl = 0x11;
    private const int Alt = 0x12;

    private static readonly TimeSpan Arrival = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan MissedMove = TimeSpan.FromMilliseconds(200);

    private Hold? _hold;
    private bool _busy;

    private (POINT Back, POINT At)? _away;

    private enum Given
    {
        Back,

        BackStillHovered,

        Taken
    }

    public async Task<string> Point(FrameworkElement element, string name)
    {
        using var turn = Turn();
        await Release();
        var target = Aim(element, name);
        RefuseWhileUsed();
        var back = CursorNow();
        var landed = await Arrive(target, back);
        var hold = new Hold(target, back, landed);
        _hold = hold;
        _away = null;
        hold.Motion = new Sighting(target.Element, () => Moved(hold),
            UIElement.PointerMovedEvent, UIElement.PointerExitedEvent);
        hold.Timer = loop.Schedule(HoldLimit, () => ExpireIfHeld(hold));
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
            using var released = new Sighting(element, UIElement.PointerReleasedEvent);
            Sent(PointerInput.Press(landed, right));
            if (!await SeenWithin(released.Seen, Arrival))
                throw new IpcException(
                    $"{name} didn't see the press within {Arrival.TotalSeconds:0} seconds, so nothing more was sent.");
            await LetPressedLookApply();
        }
        finally
        {
            given = await GiveBack(target, back, landed);
        }

        return (right ? $"Right-pressed {name}" : $"Pressed {name}") + Afterwards(given);
    }

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
                if (!await SeenWithin(turned.Seen, Arrival))
                    throw new IpcException(
                        $"{name} didn't see the wheel turn within {Arrival.TotalSeconds:0} seconds, so nothing more was sent.");
            }
        }
        finally
        {
            given = await GiveBack(target, back, landed);
        }

        return $"Scrolled {name} {(steps > 0 ? "down" : "up")} {Math.Abs(steps)}" + Afterwards(given);
    }

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
        var root = KeyTarget(shown);
        foreach (var chord in sequence.Chords)
        {
            using var struck = new Sighting(root, UIElement.KeyUpEvent);
            if (chord.Modifiers == HotkeyModifiers.None)
            {
                if (!InputSite.PostKey(site, chord.VirtualKey))
                    throw new IpcException("Windows took none or only part of the input.");
            }
            else
            {
                if (!Ours(TopLevelWindows.RootOf(PInvoke.GetForegroundWindow()), window))
                    throw new IpcException(
                        $"{chord.Text} holds a modifier, which only the real keyboard carries, and Windows gives that to the window in front. This window isn't in front, so nothing more was sent. A plain key, such as Tab or Enter, reaches any window.");
                RefuseWhileUsed();
                Sent(KeyInput.Chord((int[])[.. Modifiers(chord.Modifiers), chord.VirtualKey]));
            }

            if (!await SeenWithin(struck.Seen, Arrival))
                throw new IpcException(
                    $"{chord.Text} wasn't seen within {Arrival.TotalSeconds:0} seconds, so nothing more was sent.");
        }

        return $"Pressed {sequence.Text}";
    }

    public void Dispose()
    {
        var hold = _hold;
        _hold = null;
        hold?.Timer?.Dispose();
        hold?.Motion?.Dispose();
        var away = _away ?? (hold is null ? null : (hold.Back, hold.At));
        if (away is var (back, at) && IsAt(at))
            PutBack(back);
        _away = null;
    }

    private static bool Taken(HotkeyChord chord)
    {
        var alt = (chord.Modifiers & HotkeyModifiers.Alt) != 0;
        var ctrl = (chord.Modifiers & HotkeyModifiers.Ctrl) != 0;
        if ((chord.VirtualKey == Escape && (alt || ctrl)) || (chord.VirtualKey == Tab && alt))
            return true;
        return chord.Modifiers != HotkeyModifiers.None &&
               !KeyInput.Unclaimed((uint)chord.Modifiers, chord.VirtualKey);
    }

    private static string Afterwards(Given given)
    {
        return given switch
        {
            Given.Taken => ". The mouse moved, so the cursor stays where it is.",
            Given.BackStillHovered => ". It may keep its hover look until the mouse moves.",
            _ => ""
        };
    }

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

    private static UIElement KeyTarget(ShellWindow shown)
    {
        return shown.Content?.XamlRoot?.Content as UIElement
            ?? throw new IpcException("That window shows nothing, so nothing was sent.");
    }

    private static POINT CursorNow()
    {
        return PointerInput.Cursor() ?? throw new IpcException("Windows won't say where the cursor is.");
    }

    private static bool IsAt(POINT point)
    {
        return PointerInput.Cursor() is { } now && Near(now, point);
    }

    private static bool Near(POINT one, POINT other)
    {
        return Math.Abs(one.X - other.X) <= 1 && Math.Abs(one.Y - other.Y) <= 1;
    }

    private static bool Ours(nint root, nint window)
    {
        return root == window || (nint)PInvoke.GetWindow((HWND)root, GET_WINDOW_CMD.GW_OWNER) == window;
    }

    private static POINT? OnScreen(nint window, XamlRoot root, Point at)
    {
        var point = new System.Drawing.Point(
            (int)Math.Round(at.X * root.RasterizationScale),
            (int)Math.Round(at.Y * root.RasterizationScale));
        return PInvoke.ClientToScreen((HWND)window, ref point)
            ? new POINT { X = point.X, Y = point.Y }
            : null;
    }

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

    private async Task<POINT> Arrive(Target target, POINT back)
    {
        var at = target.At;
        try
        {
            using (var come = new Sighting(target.Element, UIElement.PointerEnteredEvent, UIElement.PointerMovedEvent))
            {
                RefuseAt(target, at);
                _away = (back, at);
                Sent(PointerInput.MoveTo(at));
                if (!await SeenWithin(come.Seen, MissedMove))
                {
                    var aside = at with { X = at.X + 1 };
                    RefuseAt(target, aside);
                    _away = (back, aside);
                    Sent(PointerInput.MoveTo(aside));
                    at = aside;
                }

                if (!await SeenWithin(come.Seen, Arrival))
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

    private static void PutBack(POINT back)
    {
        PointerInput.MoveTo(back, asMouse: false);
    }

    private async Task<bool> SeenWithin(Task seen, TimeSpan limit)
    {
        try
        {
            await seen.WaitAsync(limit, loop.Time);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static async Task LetPressedLookApply()
    {
        var queue = DispatcherQueue.GetForCurrentThread();
        if (queue is null)
            return;
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!queue.TryEnqueue(DispatcherQueuePriority.Low, () => done.TrySetResult()))
            return;
        await done.Task;
    }

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

    private static void RefuseAt(Target target, POINT at)
    {
        RefuseWhileUsed();
        if (!Ours(PointerInput.RootWindowAt(at), target.Window))
            throw new IpcException(
                $"Another window is over {target.Name} at ({at.X}, {at.Y}), and real input there would reach it. Nothing was sent.");
    }

    private static void RefuseWhileUsed()
    {
        switch (SentInput.UsedWithinQuiet())
        {
            case null:
                throw new IpcException("Windows won't say when the PC was last used, so nothing was sent.");
            case true:
                throw new IpcException(
                    $"Someone used the mouse or keyboard in the last {SentInput.Quiet.TotalSeconds:0} seconds, and real input would get in their way. Nothing was sent; try again once the PC is left alone.");
        }
    }

    private static void Sent(bool all)
    {
        if (!all)
            throw new IpcException("Windows took none or only part of the input.");
    }

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
                stepped = await SeenWithin(go.Seen, Arrival);
            }
            catch (IpcException)
            {
            }
        }

        _away = null;
        if (!IsAt(now))
            return Given.Taken;
        PutBack(back);
        return stepped ? Given.Back : Given.BackStillHovered;
    }

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

    private async Task<Given?> Release()
    {
        if (_hold is not { } hold)
            return null;
        _hold = null;
        hold.Timer?.Dispose();
        hold.Motion?.Dispose();
        return await GiveBack(hold.Target, hold.Back, hold.At);
    }

    private async Task Expire()
    {
        using var turn = Turn();
        await Release();
    }

    private void ExpireIfHeld(Hold hold)
    {
        if (!ReferenceEquals(_hold, hold) || _busy)
            return;
        if (!IsAt(hold.At))
        {
            Drop(hold);
            return;
        }

        _ = Expire();
    }

    private void Moved(Hold hold)
    {
        if (!ReferenceEquals(_hold, hold) || _busy || IsAt(hold.At))
            return;
        Drop(hold);
    }

    private void Drop(Hold hold)
    {
        _hold = null;
        hold.Timer?.Dispose();
        var motion = hold.Motion;
        hold.Motion = null;
        loop.Post(() => motion?.Dispose());
    }

    private sealed record Target(FrameworkElement Element, string Name, nint Window, POINT At);

    private sealed record Hold(Target Target, POINT Back, POINT At)
    {
        public IDisposable? Timer { get; set; }

        public Sighting? Motion { get; set; }
    }

    private sealed partial class Turned(RealInput input) : IDisposable
    {
        public void Dispose()
        {
            input._busy = false;
        }
    }

    private sealed partial class Sighting : IDisposable
    {
        private readonly UIElement _element;
        private readonly RoutedEvent[] _events;
        private readonly PointerEventHandler _pointer;
        private readonly KeyEventHandler _key;
        private readonly TaskCompletionSource _seen = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Sighting(UIElement element, params RoutedEvent[] events)
            : this(element, null, events)
        {
        }

        public Sighting(UIElement element, Action? each, params RoutedEvent[] events)
        {
            _element = element;
            _events = events;
            void Note()
            {
                _seen.TrySetResult();
                each?.Invoke();
            }

            _pointer = (_, _) => Note();
            _key = (_, _) => Note();
            foreach (var routed in events)
                element.AddHandler(routed, Handler(routed), true);
        }

        public Task Seen => _seen.Task;

        public void Dispose()
        {
            foreach (var routed in _events)
                _element.RemoveHandler(routed, Handler(routed));
        }

        private object Handler(RoutedEvent routed)
        {
            return routed == UIElement.KeyDownEvent || routed == UIElement.KeyUpEvent ? _key : _pointer;
        }
    }
}
