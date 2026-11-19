using Winshell.Apps;
using Winshell.Native;

namespace Winshell.Windows;

internal sealed record WindowInfo(
    nint Hwnd,
    string Title,
    string AppId,
    string Executable,
    string Icon,
    nint Owner,
    bool Maximized,
    bool Minimized,
    bool Fullscreen,
    string Monitor);

internal readonly record struct WindowIdentity(
    string AppId,
    string Executable,
    string Icon,
    string PackageAppId,
    bool PackageKnown);

public sealed class WindowListService : ObservableObject, IDisposable
{
    private readonly IEventLoop _loop;
    private readonly Log _log;
    private readonly IWindowBackend _backend;
    private readonly IDisposable _winEvents;
    private readonly IDisposable _flashes;
    private readonly VirtualDesktopsService? _desktops;
    private readonly Dictionary<nint, AppWindow> _byHwnd = [];
    private readonly Dictionary<nint, WindowIdentity> _identities = [];
    private readonly HashSet<nint> _dirty = [];
    private readonly HashSet<nint> _flashed = [];
    private readonly HashSet<nint> _wanting = [];
    private AppWindow? _activeWindow;
    private bool _flushPosted;
    private bool _foregroundDirty;
    private bool _disposed;

    internal WindowListService(IEventLoop loop, Log log, VirtualDesktopsService? desktops = null,
        IWindowBackend? backend = null)
    {
        _loop = loop;
        _log = log;
        _desktops = desktops;
        _backend = backend ?? WindowBackend.Shared;
        _winEvents = _backend.ListenWinEvents(OnWinEvent, Failed);
        _flashes = _backend.ListenFlashes(OnFlashed, Failed);
        foreach (var refused in _backend.Start())
            log.Warn("windows", refused);
        if (_desktops is not null)
            _desktops.Refreshed += OnDesktopsRefreshed;
        foreach (var hwnd in _backend.TopLevel())
            _dirty.Add(hwnd);
        _foregroundDirty = true;
        Flush();
    }

    public ObservableList<AppWindow> Windows { get => Read(field); } = [];

    public AppWindow? ActiveWindow => Read(_activeWindow);

    internal AppWindow? Find(nint hwnd)
    {
        return _byHwnd.GetValueOrDefault(hwnd);
    }

    private void OnWinEvent(uint eventType, nint hwnd, int idObject, int idChild)
    {
        if (eventType == PInvoke.EVENT_OBJECT_LOCATIONCHANGE && !WindowRules.IsWindowObject(idObject, idChild))
            return;
        var isRoot = hwnd != 0 && eventType != PInvoke.EVENT_OBJECT_DESTROY && _backend.RootOf(hwnd) == hwnd;
        var marksWindow =
            WindowRules.MarksWindow(eventType, hwnd, idObject, idChild, _byHwnd.ContainsKey(hwnd), isRoot);
        var marksForeground = WindowRules.MarksForeground(eventType);
        if (!marksWindow && !marksForeground)
            return;
        if (marksWindow)
            _dirty.Add(hwnd);
        _foregroundDirty |= marksForeground;
        PostFlush();
    }

    private void Failed(Exception e)
    {
        _log.Warn("windows", e.Message);
    }

    private void OnFlashed(nint hwnd)
    {
        _flashed.Add(hwnd);
        PostFlush();
    }

    private void PostFlush()
    {
        if (_flushPosted)
            return;
        _flushPosted = true;
        _loop.Post(Flush);
    }

    private void Flush()
    {
        _flushPosted = false;
        if (_disposed)
            return;
        var added = new List<AppWindow>();
        foreach (var hwnd in _dirty.ToArray())
        {
            _dirty.Remove(hwnd);
            var info = Read(hwnd);
            _byHwnd.TryGetValue(hwnd, out var window);
            if (info is null)
            {
                _identities.Remove(hwnd);
                if (window is not null)
                    Remove(window);
            }
            else if (window is null)
            {
                window = new AppWindow(info, _backend, _desktops, _log);
                _byHwnd[hwnd] = window;
                added.Add(window);
            }
            else
            {
                window.Update(info);
            }
        }

        foreach (var window in _byHwnd.Values)
            window.SetParent(window.Owner == 0 ? null : _byHwnd.GetValueOrDefault(window.Owner));
        foreach (var window in added)
            Windows.Add(window);
        if (_foregroundDirty)
        {
            _foregroundDirty = false;
            Set(ref _activeWindow, ActiveOf(_backend.Foreground), nameof(ActiveWindow));
        }

        foreach (var hwnd in _flashed)
            if (ActiveOf(hwnd) is { } wanting && wanting != _activeWindow)
                _wanting.Add(wanting.Hwnd);
        _flashed.Clear();
        WindowRules.SettleAttention(_wanting, _activeWindow?.Hwnd ?? 0, _byHwnd.ContainsKey);
        foreach (var window in _byHwnd.Values)
        {
            window.SetActivated(window == _activeWindow);
            window.SetAttention(_wanting.Contains(window.Hwnd));
        }
    }

    private void Remove(AppWindow window)
    {
        _byHwnd.Remove(window.Hwnd);
        Windows.Remove(window);
        if (_activeWindow == window)
            Set(ref _activeWindow, null, nameof(ActiveWindow));
        window.NotifyClosed();
    }

    private AppWindow? ActiveOf(nint hwnd)
    {
        hwnd = hwnd == 0 ? 0 : _backend.RootOf(hwnd);
        for (var depth = 0; hwnd != 0 && depth < 16; depth++)
        {
            if (_byHwnd.TryGetValue(hwnd, out var window))
                return window;
            hwnd = _backend.OwnerOf(hwnd);
        }

        return null;
    }

    private WindowInfo? Read(nint hwnd)
    {
        if (!ShowsOnTaskbar(_backend.TaskbarFacts(hwnd)))
            return null;
        _identities.TryGetValue(hwnd, out var previous);
        var read = _backend.Read(hwnd, !previous.PackageKnown, previous.Executable is not { Length: > 0 });
        var identity = Resolve(previous, read.AppUserModelId, read.PackageAppId, read.Executable);
        _identities[hwnd] = identity;
        return new WindowInfo(hwnd, read.Title, identity.AppId, identity.Executable, identity.Icon,
            _backend.OwnerOf(hwnd),
            read.Maximized, read.Minimized, read.Fullscreen, read.Monitor);
    }

    private static bool ShowsOnTaskbar(TaskbarWindowFacts window)
    {
        return window is { Exists: true, Visible: true, Shell: false, Child: false, HasTitle: true, Cloaked: false }
               && (window.AppWindow || window is { Owned: false, ToolWindow: false, NoActivate: false });
    }

    private static WindowIdentity Resolve(WindowIdentity previous, string windowProperty, string? packageAppId,
        string? executable)
    {
        var package = packageAppId ?? previous.PackageAppId ?? "";
        var path = executable ?? previous.Executable ?? "";
        var source = windowProperty.Length > 0 ? windowProperty : package;
        return new WindowIdentity(
            WindowRules.AppId(windowProperty, package, path),
            path,
            AppIdentity.IconPath(source, path),
            package,
            previous.PackageKnown || packageAppId is not null);
    }

    private void OnDesktopsRefreshed()
    {
        foreach (var window in _byHwnd.Values)
            window.NotifyDesktop();
    }

    void IDisposable.Dispose()
    {
        _disposed = true;
        _winEvents.Dispose();
        _flashes.Dispose();
        if (_desktops is not null)
            _desktops.Refreshed -= OnDesktopsRefreshed;
    }
}

public sealed class AppWindow : ObservableObject
{
    private readonly IWindowBackend _backend;
    private readonly VirtualDesktopsService? _desktops;
    private readonly Log _log;
    private WindowInfo _info;
    private AppWindow? _parent;
    private bool _activated;
    private bool _attention;

    internal AppWindow(WindowInfo info, IWindowBackend backend, VirtualDesktopsService? desktops, Log log)
    {
        _info = info;
        _backend = backend;
        _desktops = desktops;
        _log = log;
    }

    internal nint Hwnd => _info.Hwnd;

    internal nint Owner => _info.Owner;

    public string Title => Read(_info).Title;

    public string AppId => Read(_info).AppId;

    public string Executable => Read(_info).Executable;

    public string Icon => Read(_info).Icon;

    public string DesktopId => Read(_backend).DesktopId(Hwnd);

    public VirtualDesktop? Desktop => Read(_desktops)?.Find(DesktopId);

    public bool Activated => Read(_activated);

    public bool Attention => Read(_attention);

    public AppWindow? Parent => Read(_parent);

    public IReadOnlyList<ScreenInfo> Screens => Read(_backend).Screen(_info.Monitor) is { } screen ? [screen] : [];

    public bool Maximized
    {
        get => Read(_info).Maximized;
        set => _backend.SetMaximized(Hwnd, value);
    }

    public bool Minimized
    {
        get => Read(_info).Minimized;
        set => _backend.SetMinimized(Hwnd, value);
    }

    public bool Fullscreen => Read(_info).Fullscreen;

    public event Action? Closed;

    public void Activate()
    {
        _backend.Activate(Hwnd);
    }

    public void Close()
    {
        _backend.Close(Hwnd);
    }

    public void MoveTo(ScreenInfo? screen)
    {
        _backend.MoveToMonitor(Hwnd, screen?.Id ?? "");
    }

    public void MoveToDesktop(VirtualDesktop? desktop)
    {
        if (desktop is null || !Guid.TryParse(desktop.Id, out var id) || id == Guid.Empty)
            return;
        if (_backend.MoveToDesktop(Hwnd, id, out var problem))
            NotifyDesktop();
        else
            _log.Warn("desktops", problem);
    }

    internal void NotifyDesktop()
    {
        Changed(nameof(DesktopId));
        Changed(nameof(Desktop));
    }

    internal void NotifyClosed()
    {
        Closed?.Invoke();
    }

    internal void Update(WindowInfo info)
    {
        var old = _info;
        _info = info;
        Changed(old.Title != info.Title, nameof(Title));
        Changed(old.AppId != info.AppId, nameof(AppId));
        Changed(old.Executable != info.Executable, nameof(Executable));
        Changed(old.Icon != info.Icon, nameof(Icon));
        Changed(old.Maximized != info.Maximized, nameof(Maximized));
        Changed(old.Minimized != info.Minimized, nameof(Minimized));
        Changed(old.Fullscreen != info.Fullscreen, nameof(Fullscreen));
        Changed(old.Monitor != info.Monitor, nameof(Screens));
    }

    internal void SetParent(AppWindow? parent)
    {
        Set(ref _parent, parent, nameof(Parent));
    }

    internal void SetActivated(bool activated)
    {
        Set(ref _activated, activated, nameof(Activated));
    }

    internal void SetAttention(bool attention)
    {
        Set(ref _attention, attention, nameof(Attention));
    }

    public override string ToString()
    {
        return $"AppWindow({_info.AppId}: {_info.Title})";
    }
}
