using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell.AppWindows;

internal readonly record struct TaskbarWindowFacts(
    bool Exists,
    bool Visible,
    bool Shell,
    bool Child,
    bool AppWindow,
    bool Owned,
    bool ToolWindow,
    bool NoActivate,
    bool HasTitle,
    bool Cloaked);

internal sealed record WindowRead(
    string Title,
    bool Maximized,
    bool Minimized,
    bool Fullscreen,
    string Monitor,
    string AppUserModelId,
    string? PackageAppId,
    string? Executable);

internal interface IWindowBackend
{
    IDisposable ListenWinEvents(Action<uint, nint, int, int> handler, Action<Exception> failed);

    IDisposable ListenFlashes(Action<nint> handler, Action<Exception> failed);

    nint Foreground { get; }

    IReadOnlyList<string> Start();

    IReadOnlyList<string> WatchForeground();

    IReadOnlySet<string> FullscreenScreens();

    IReadOnlyList<nint> TopLevel();

    nint OwnerOf(nint hwnd);

    nint RootOf(nint hwnd);

    TaskbarWindowFacts TaskbarFacts(nint hwnd);

    WindowRead Read(nint hwnd, bool package, bool executable);

    ScreenInfo? Screen(string deviceName);

    string DesktopId(nint hwnd);

    bool MoveToDesktop(nint hwnd, Guid desktop, out string problem);

    void Activate(nint hwnd);

    void Close(nint hwnd);

    void SetMaximized(nint hwnd, bool maximized);

    void SetMinimized(nint hwnd, bool minimized);

    void MoveToMonitor(nint hwnd, string deviceName);
}

internal sealed class WindowBackend : IWindowBackend
{
    public static WindowBackend Shared { get; } = new();

    private readonly Listeners<Action<uint, nint, int, int>> _winEvents = new();
    private readonly Listeners<Action<nint>> _flashes = new();
    private bool _started;
    private bool _foregroundWatched;

    private WindowBackend()
    {
    }

    public IDisposable ListenWinEvents(Action<uint, nint, int, int> handler, Action<Exception> failed)
    {
        return _winEvents.Add(handler, failed);
    }

    public IDisposable ListenFlashes(Action<nint> handler, Action<Exception> failed)
    {
        return _flashes.Add(handler, failed);
    }

    public nint Foreground => PInvoke.GetForegroundWindow();

    public IReadOnlyList<nint> TopLevel()
    {
        return TopLevelWindows.List();
    }

    public nint OwnerOf(nint hwnd)
    {
        return PInvoke.GetWindow((HWND)hwnd, GET_WINDOW_CMD.GW_OWNER);
    }

    public nint RootOf(nint hwnd)
    {
        return TopLevelWindows.RootOf(hwnd);
    }

    public ScreenInfo? Screen(string deviceName)
    {
        return Screens.Find(deviceName) is { } monitor ? Screens.Describe(monitor).Info : null;
    }

    public string DesktopId(nint hwnd)
    {
        return WindowDesktop.IdOf(hwnd);
    }

    public bool MoveToDesktop(nint hwnd, Guid desktop, out string problem)
    {
        return WindowDesktop.Move(hwnd, desktop, out problem);
    }

    public IReadOnlyList<string> WatchForeground()
    {
        List<string> refused = [];
        if (_foregroundWatched)
            return refused;
        _foregroundWatched = true;
        Hook(refused, PInvoke.EVENT_SYSTEM_FOREGROUND, PInvoke.EVENT_SYSTEM_FOREGROUND);
        Hook(refused, PInvoke.EVENT_OBJECT_LOCATIONCHANGE, PInvoke.EVENT_OBJECT_LOCATIONCHANGE);
        return refused;
    }

    public IReadOnlyList<string> Start()
    {
        if (_started)
            return [];
        _started = true;
        var refused = WatchForeground().ToList();
        Hook(refused, PInvoke.EVENT_SYSTEM_MINIMIZESTART, PInvoke.EVENT_SYSTEM_MINIMIZEEND);
        Hook(refused, PInvoke.EVENT_OBJECT_CREATE, PInvoke.EVENT_OBJECT_HIDE);
        Hook(refused, PInvoke.EVENT_OBJECT_NAMECHANGE, PInvoke.EVENT_OBJECT_NAMECHANGE);
        Hook(refused, PInvoke.EVENT_OBJECT_CLOAKED, PInvoke.EVENT_OBJECT_UNCLOAKED);
        if (!ShellHookWindow.Start(OnShellHook))
            refused.Add("RegisterShellHookWindow failed; windows that flash will not show that they want attention");
        return refused;
    }

    private void Hook(List<string> refused, uint min, uint max)
    {
        if (WinEventHook.Start(min, max, OnWinEvent) is null)
            refused.Add($"SetWinEventHook failed for events {min:x}-{max:x}; those changes will not be seen");
    }

    private void OnShellHook(nint code, nint hwnd)
    {
        if (WindowRules.IsFlash(code) && hwnd != 0)
            _flashes.Raise(hwnd, static (handler, window) => handler(window));
    }

    private void OnWinEvent(uint eventType, nint hwnd, int idObject, int idChild)
    {
        _winEvents.Raise((eventType, hwnd, idObject, idChild),
            static (handler, e) => handler(e.eventType, e.hwnd, e.idObject, e.idChild));
    }

    public TaskbarWindowFacts TaskbarFacts(nint hwnd)
    {
        if (!PInvoke.IsWindowVisible((HWND)hwnd))
            return new TaskbarWindowFacts(PInvoke.IsWindow((HWND)hwnd), false, false, false, false, false, false, false, false,
                false);
        var ex = (long)PInvoke.GetWindowLongPtr((HWND)hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        return new TaskbarWindowFacts(
            PInvoke.IsWindow((HWND)hwnd),
            true,
            TopLevelWindows.IsShellWindow(hwnd),
            TopLevelWindows.HasChildStyle(hwnd),
            (ex & (uint)WINDOW_EX_STYLE.WS_EX_APPWINDOW) != 0,
            PInvoke.GetWindow((HWND)hwnd, GET_WINDOW_CMD.GW_OWNER) != 0,
            (ex & (uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) != 0,
            (ex & (uint)WINDOW_EX_STYLE.WS_EX_NOACTIVATE) != 0,
            TopLevelWindows.HasTitle(hwnd),
            TopLevelWindows.IsCloaked(hwnd));
    }

    public WindowRead Read(nint hwnd, bool package, bool executable)
    {
        var minimized = PInvoke.IsIconic((HWND)hwnd);
        var maximized = !minimized && PInvoke.IsZoomed((HWND)hwnd);
        var monitor = Screens.Of(hwnd);
        var device = monitor?.Id ?? "";
        var fullscreen = !minimized && monitor is not null && PInvoke.GetWindowRect((HWND)hwnd, out var rect) &&
                         WindowRules.CoversMonitor(PInvoke.GetWindowLongPtr((HWND)hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE), rect,
                             monitor.Bounds);

        var property = AppUserModelIds.OfWindow(hwnd);
        string? packageAppId = null;
        if (property.Length == 0 && package)
        {
            PInvoke.GetWindowThreadProcessId((HWND)hwnd, out var pid);
            packageAppId = AppUserModelIds.OfProcess(pid);
        }

        return new WindowRead(TopLevelWindows.Title(hwnd), maximized, minimized, fullscreen, device, property,
            packageAppId, executable ? ExePath(hwnd) : null);
    }

    private static string ExePath(nint hwnd)
    {
        PInvoke.GetWindowThreadProcessId((HWND)hwnd, out var pid);
        return ProcessImage.TryRead(pid) ?? "";
    }

    public IReadOnlySet<string> FullscreenScreens()
    {
        HashSet<string> screens = [];
        var hwnd = PInvoke.GetForegroundWindow();
        if (hwnd == 0 || !PInvoke.IsWindowVisible(hwnd) || TopLevelWindows.IsShellWindow(hwnd)
            || (PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE) & (uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) != 0
            || !PInvoke.GetWindowRect(hwnd, out var rect))
            return screens;
        var style = (long)PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        foreach (var monitor in Screens.List())
            if (WindowRules.CoversMonitor(style, rect, monitor.Bounds))
                screens.Add(monitor.Id);
        return screens;
    }

    public void MoveToMonitor(nint hwnd, string deviceName)
    {
        if (deviceName.Length == 0 || !PInvoke.IsWindow((HWND)hwnd))
            return;
        if (Screens.Find(deviceName) is not { } target)
            return;
        var source = Screens.Of(hwnd);
        if (source is null || source.Id == target.Id)
            return;
        if (!PInvoke.GetWindowRect((HWND)hwnd, out var current))
            return;
        var hasPlacement = TopLevelWindows.TryPlacement(hwnd, out var placement);
        var minimized = PInvoke.IsIconic((HWND)hwnd);
        var maximized = !minimized && PInvoke.IsZoomed((HWND)hwnd);
        var primary = Screens.Primary();
        var window = new WindowToMove(current, hasPlacement ? placement.rcNormalPosition : default, hasPlacement, minimized,
            maximized,
            (PInvoke.GetWindowLongPtr((HWND)hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE) & (uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) != 0);
        if (!WindowOnScreen.TryMove(window, source.Frame, target.Frame, primary?.Bounds ?? default,
                primary?.Work ?? default, out var place))
            return;
        if (hasPlacement)
            TopLevelWindows.Place(hwnd, placement, place.Workspace, minimized, maximized);
        else
            TopLevelWindows.Move(hwnd, place.Screen);
    }

    public void Activate(nint hwnd)
    {
        if (PInvoke.IsIconic((HWND)hwnd))
            TopLevelWindows.Restore(hwnd);
        Focus.Take(hwnd);
    }

    public void Close(nint hwnd)
    {
        TopLevelWindows.Close(hwnd);
    }

    public void SetMaximized(nint hwnd, bool maximized)
    {
        if (maximized)
            TopLevelWindows.Maximize(hwnd);
        else
            TopLevelWindows.Restore(hwnd);
    }

    public void SetMinimized(nint hwnd, bool minimized)
    {
        if (minimized)
            TopLevelWindows.Minimize(hwnd);
        else
            TopLevelWindows.Restore(hwnd);
    }
}
