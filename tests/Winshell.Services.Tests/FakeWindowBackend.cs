using Winshell.Native;
using Winshell.AppWindows;

namespace Winshell.Services.Tests;

internal sealed class FakeWindowBackend : IWindowBackend
{
    public Dictionary<nint, WindowRead> Shown { get; } = [];

    private Dictionary<nint, TaskbarWindowFacts> Facts { get; } = [];

    public Dictionary<nint, nint> Owners { get; } = [];

    public Dictionary<string, ScreenInfo> Screens { get; } = [];

    public Dictionary<nint, string> DesktopIds { get; } = [];

    public List<string> Calls { get; } = [];

    public nint Foreground { get; set; }

    public HashSet<string> Fullscreen { get; } = [];

    public Action<nint>? Reading { get; set; }

    private readonly Listeners<Action<uint, nint, int, int>> _winEvents = new();
    private readonly Listeners<Action<nint>> _flashes = new();
    private int _listening;

    public bool HasListeners => _listening > 0;

    public IDisposable ListenWinEvents(Action<uint, nint, int, int> handler, Action<Exception> failed)
    {
        return Counted(_winEvents.Add(handler, failed));
    }

    public IDisposable ListenFlashes(Action<nint> handler, Action<Exception> failed)
    {
        return Counted(_flashes.Add(handler, failed));
    }

    private DisposeAction Counted(IDisposable listening)
    {
        _listening++;
        return new DisposeAction(() =>
        {
            _listening--;
            listening.Dispose();
        });
    }

    public static TaskbarWindowFacts Normal { get; } =
        new(true, true, false, false, false, false, false, false, true, false);

    public static WindowRead Window(string title = "Title")
    {
        return new WindowRead(title, false, false, false, "M1", "", "", @"C:\app.exe");
    }

    public void Show(nint hwnd, string title = "Title")
    {
        Show(hwnd, Window(title));
    }

    public void Show(nint hwnd, WindowRead read, TaskbarWindowFacts? facts = null)
    {
        Shown[hwnd] = read;
        Facts[hwnd] = facts ?? Normal;
        Raise(PInvoke.EVENT_OBJECT_NAMECHANGE, hwnd);
    }

    public void Destroy(nint hwnd)
    {
        Shown.Remove(hwnd);
        Facts.Remove(hwnd);
        Raise(PInvoke.EVENT_OBJECT_DESTROY, hwnd);
    }

    public void Focus(nint hwnd)
    {
        Foreground = hwnd;
        Raise(PInvoke.EVENT_SYSTEM_FOREGROUND, hwnd);
    }

    public void Move(nint hwnd)
    {
        Raise(PInvoke.EVENT_OBJECT_LOCATIONCHANGE, hwnd);
    }

    public void Flash(nint hwnd)
    {
        _flashes.Raise(hwnd, static (handler, window) => handler(window));
    }

    private void Raise(uint eventType, nint hwnd)
    {
        _winEvents.Raise((eventType, hwnd),
            static (handler, e) => handler(e.eventType, e.hwnd, (int)OBJECT_IDENTIFIER.OBJID_WINDOW, (int)PInvoke.CHILDID_SELF));
    }

    public IReadOnlyList<string> Start()
    {
        return [];
    }

    public IReadOnlyList<string> WatchForeground()
    {
        return [];
    }

    public IReadOnlySet<string> FullscreenScreens()
    {
        return Fullscreen.ToHashSet();
    }

    public IReadOnlyList<nint> TopLevel()
    {
        return [.. Shown.Keys];
    }

    public nint OwnerOf(nint hwnd)
    {
        return Owners.GetValueOrDefault(hwnd);
    }

    public nint RootOf(nint hwnd)
    {
        return hwnd;
    }

    public TaskbarWindowFacts TaskbarFacts(nint hwnd)
    {
        return Facts.GetValueOrDefault(hwnd);
    }

    public WindowRead Read(nint hwnd, bool package, bool executable)
    {
        Reading?.Invoke(hwnd);
        var read = Shown[hwnd];
        return read with
        {
            PackageAppId = package && read.AppUserModelId.Length == 0 ? read.PackageAppId : null,
            Executable = executable ? read.Executable : null
        };
    }

    public ScreenInfo? Screen(string deviceName)
    {
        return Screens.GetValueOrDefault(deviceName);
    }

    public string DesktopId(nint hwnd)
    {
        return DesktopIds.GetValueOrDefault(hwnd, "");
    }

    public bool MoveToDesktop(nint hwnd, Guid desktop, out string problem)
    {
        problem = "";
        DesktopIds[hwnd] = desktop.ToString("D");
        return true;
    }

    public void Activate(nint hwnd)
    {
        Calls.Add($"activate:{hwnd}");
    }

    public void Close(nint hwnd)
    {
        Calls.Add($"close:{hwnd}");
    }

    public void SetMaximized(nint hwnd, bool maximized)
    {
        Calls.Add($"maximize:{hwnd}:{maximized}");
    }

    public void SetMinimized(nint hwnd, bool minimized)
    {
        Calls.Add($"minimize:{hwnd}:{minimized}");
    }

    public void MoveToMonitor(nint hwnd, string deviceName)
    {
        Calls.Add($"move:{hwnd}:{deviceName}");
    }
}
