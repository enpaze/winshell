using System.Collections.Specialized;
using System.Text.Json;
using Winshell.Windows;
using static Winshell.Winland.WinlandProtocol;

namespace Winshell.Winland;

public sealed class WinlandService : ObservableObject, IDisposable
{
    private readonly IEventLoop _loop;
    private readonly Log _log;
    private readonly IWinlandBackend _backend;
    private readonly IDisposable _listening;
    private readonly List<WinlandMonitor> _monitorList = [];
    private readonly List<WinlandWorkspace> _workspaceList = [];
    private readonly List<WinlandWindow> _windowList = [];
    private WindowListService? _windows;
    private WinlandMonitor? _focusedMonitor;
    private WinlandWorkspace? _focusedWorkspace;
    private WinlandWindow? _activeWindow;
    private string? _configPath, _logPath;
    private QueryState _monitorQuery = new(), _workspaceQuery = new(), _windowQuery = new();
    private bool _monitorsRequested;
    private bool _disposed;

    private int _generation;

    internal WinlandService(IEventLoop loop, Log log, IWinlandBackend? backend = null)
    {
        _loop = loop;
        _log = log;
        _backend = backend ?? WinlandBackend.Shared;
        _listening = _backend.Listen(OnConnectionChanged, OnEventReceived, e => _log.Warn("winland", e.ToString()));
        if (_backend.IsConnected)
            RefreshAll();
    }

    public event Action<WinlandEvent>? RawEvent;

    public ObservableList<WinlandMonitor> Monitors { get => Read(field); } = [];

    public ObservableList<WinlandWorkspace> Workspaces { get => Read(field); } = [];

    public ObservableList<WinlandWindow> Windows { get => Read(field); } = [];

    public WinlandMonitor? FocusedMonitor => Read(_focusedMonitor);

    public WinlandWorkspace? FocusedWorkspace => Read(_focusedWorkspace);

    public WinlandWindow? ActiveWindow => Read(_activeWindow);

    public string PipePath => Read(_backend).PipePath;

    public int ProcessId => Read(_backend).ProcessId;

    public string? ConfigPath => Read(_configPath);

    public string? LogPath => Read(_logPath);

    internal void Attach(WindowListService? windows)
    {
        if (ReferenceEquals(_windows, windows))
            return;
        if (_windows is not null)
            _windows.Windows.CollectionChanged -= OnWindowsChanged;
        _windows = windows;
        if (windows is not null)
            windows.Windows.CollectionChanged += OnWindowsChanged;
        foreach (var window in _windowList)
            window.NotifyWindow();
    }

    internal AppWindow? FindWindow(nint handle)
    {
        return _windows?.Find(handle);
    }

    private void OnWindowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var window in _windowList)
            window.NotifyWindow();
    }

    public void Dispatch(string request)
    {
        Send("dispatch " + request);
    }

    public void Reload()
    {
        Send("reload");
    }

    public void RefreshMonitors()
    {
        RefreshMonitors(false);
    }

    public void RefreshWorkspaces()
    {
        RefreshWorkspaces(false);
    }

    public void RefreshWindows()
    {
        Ask(_windowQuery, "j/clients", false, (json, _) =>
        {
            foreach (var entry in json.EnumerateArray())
            {
                var address = ParseAddress(Str(entry, "address"));
                if (address == 0)
                    continue;
                var window = FindWindow(address, true)!;
                window.SetTitle(Str(entry, "title"));
                var ws = Obj(entry, "workspace");
                if (FindWorkspace(Str(ws, "name"), Str(ws, "monitor"), true, Int(ws, "id")) is { } workspace)
                    MoveWindow(window, workspace);
                window.SetLastIpcObject(entry);
            }
        });
    }

    public WinlandMonitor? MonitorFor(ScreenInfo? screen)
    {
        if (screen is null)
            return null;
        var monitor = _monitorList.FirstOrDefault(m => m.Name == screen.Id);
        return monitor ?? (_monitorsRequested ? null : FindMonitor(screen.Id, true));
    }

    internal void Send(string request)
    {
        Request(request, reply =>
        {
            if (reply is null)
                _log.Warn("winland", $"Failed to send \"{request}\": Winland is not running");
            else if (reply.Split("\n\n").Any(r => r.Trim() != "ok"))
                _log.Warn("winland", $"\"{request}\" failed with error {reply.Trim()}");
        });
    }

    private void OnConnectionChanged(bool connected)
    {
        _loop.Post(() =>
        {
            if (_disposed)
                return;
            if (connected)
                RefreshAll();
            else
                Reset();
            Changed(nameof(PipePath));
            Changed(nameof(ProcessId));
        });
    }

    private void OnEventReceived(string line)
    {
        _loop.Post(() =>
        {
            if (_disposed)
                return;
            var (name, data) = SplitEvent(line);
            try
            {
                OnEvent(name, data);
            }
            finally
            {
                Recompute();
            }

            RawEvent?.Invoke(new WinlandEvent(name, data));
        });
    }

    private void RefreshAll()
    {
        RefreshMonitors(true);
        RefreshWorkspaces(true);
        RefreshWindows();
        RefreshFiles();
        Request("j/activewindow", reply =>
        {
            if (_activeWindow is null && ParseJson(reply) is { ValueKind: JsonValueKind.Object } json)
                SetActiveWindow(FindWindow(ParseAddress(Str(json, "address")), true));
        });
    }

    private void Reset()
    {
        _generation++;
        _monitorQuery = new QueryState();
        _workspaceQuery = new QueryState();
        _windowQuery = new QueryState();
        _monitorsRequested = false;
        foreach (var window in _windowList.ToArray())
            RemoveWindow(window);
        foreach (var workspace in _workspaceList.ToArray())
            RemoveWorkspace(workspace);
        foreach (var monitor in _monitorList.ToArray())
            RemoveMonitor(monitor);
        SetFiles(null, null);
        Recompute();
    }

    private void RefreshFiles()
    {
        Request("j/files", reply =>
        {
            if (ParseJson(reply) is not { ValueKind: JsonValueKind.Object } json ||
                Str(json, "config") is not { Length: > 0 } config)
            {
                _ = RetryIfConnected(reply, RefreshFiles);
                return;
            }

            SetFiles(config, Str(json, "log") is { Length: > 0 } log ? log : null);
        });
    }

    private void SetFiles(string? config, string? log)
    {
        Set(ref _configPath, config, nameof(ConfigPath));
        Set(ref _logPath, log, nameof(LogPath));
    }

    private void Request(string command, Action<string?> onReply)
    {
        var generation = _generation;
        _ = _backend.RequestAsync(command).ContinueWith(t =>
        {
            var reply = t.IsCompletedSuccessfully ? t.Result : null;
            _loop.Post(() =>
            {
                if (_disposed || generation != _generation)
                    return;
                try
                {
                    onReply(reply);
                }
                finally
                {
                    Recompute();
                }
            });
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void Ask(QueryState query, string command, bool canCreate, Action<JsonElement, bool> apply)
    {
        if (query.Reading)
        {
            query.Again = query.Again == true || canCreate;
            return;
        }

        query.Reading = true;
        Request(command, reply =>
        {
            query.Reading = false;
            var again = query.Again;
            query.Again = null;
            var json = ParseJson(reply);
            if (json is not { ValueKind: JsonValueKind.Array } &&
                RetryIfConnected(reply, () => Ask(query, command, canCreate || again == true, apply)))
                return;
            if (again is { } create)
                Ask(query, command, create, apply);
            if (json is { ValueKind: JsonValueKind.Array } array)
                apply(array, canCreate);
        });
    }

    private bool RetryIfConnected(string? reply, Action retry)
    {
        if (reply is not null || !_backend.IsConnected)
            return false;
        var generation = _generation;
        _loop.Schedule(TimeSpan.FromSeconds(1), () =>
        {
            if (!_disposed && generation == _generation)
                retry();
        });
        return true;
    }

    private void OnEvent(string name, string data)
    {
        switch (ReadEvent(name, data))
        {
            case null:
                break;
            case WinlandProtocol.ConfigReloaded:
                RefreshAll();
                break;
            case WinlandProtocol.MonitorAdded added:
                {
                    var monitor = FindMonitor(added.Name, true)!;
                    monitor.UpdateInitial(added.Id, added.Name, added.Description);
                    RefreshMonitors(false);
                    break;
                }
            case WinlandProtocol.MonitorRemoved removed:
                if (FindMonitor(removed.Name, false) is { } gone)
                    RemoveMonitor(gone);
                break;
            case WinlandProtocol.WorkspaceCreated created:
                {
                    var existed = FindWorkspace(created.Name, created.Monitor, false) is not null;
                    var workspace = FindWorkspace(created.Name, created.Monitor, true, created.Id)!;
                    workspace.SetId(created.Id);
                    if (!existed)
                        RefreshWorkspaces(false);
                    break;
                }
            case WinlandProtocol.WorkspaceDestroyed destroyed:
                {
                    if (FindWorkspace(destroyed.Name, destroyed.Monitor, false) is { } workspace)
                        RemoveWorkspace(workspace);
                    if (_monitorList.Any(m => m.ActiveWorkspace is null))
                        RefreshMonitors(false);
                    break;
                }
            case WinlandProtocol.MonitorFocused focused:
                {
                    var monitor = FindMonitor(focused.Monitor, true)!;
                    var workspace = focused.Workspace is null
                        ? null
                        : FindWorkspace(focused.Workspace, focused.Monitor, false);
                    SetFocusedMonitor(monitor);
                    monitor.SetActiveWorkspace(workspace);
                    break;
                }
            case WinlandProtocol.WorkspaceShown shown:
                {
                    var monitor = shown.Monitor.Length > 0 ? FindMonitor(shown.Monitor, true) : _focusedMonitor;
                    if (monitor is not null)
                        monitor.SetActiveWorkspace(FindWorkspace(shown.Name, monitor.Name, true, shown.Id));
                    break;
                }
            case WinlandProtocol.WorkspaceMoved moved:
                {
                    var monitor = FindMonitor(moved.Monitor, true)!;
                    var workspace = FindWorkspace(moved.Name, moved.Monitor, false)
                                    ?? _workspaceList.FirstOrDefault(w => w.Name == moved.Name && w.Monitor != monitor)
                                    ?? FindWorkspace(moved.Name, moved.Monitor, true, moved.Id)!;
                    workspace.SetMonitor(monitor);
                    RefreshWorkspaces(false);
                    RefreshMonitors(false);
                    break;
                }
            case WinlandProtocol.FullscreenChanged fullscreen:
                _focusedWorkspace?.SetHasFullscreen(fullscreen.Fullscreen);
                RefreshWorkspaces(false);
                break;
            case WinlandProtocol.WindowOpened opened:
                {
                    if (opened.Address == 0)
                        return;
                    if (FindWorkspace(opened.Workspace, opened.Monitor, false) is not { } workspace)
                    {
                        _log.Debug("winland",
                            $"openwindow for workspace {opened.Workspace} which was not previously tracked");
                        return;
                    }

                    var window = FindWindow(opened.Address, true)!;
                    window.SetTitle(opened.Title);
                    MoveWindow(window, workspace);
                    break;
                }
            case WinlandProtocol.WindowClosed closed:
                if (FindWindow(closed.Address, false) is { } closedWindow)
                    RemoveWindow(closedWindow);
                break;
            case WinlandProtocol.WindowMoved movedWindow:
                if (FindWindow(movedWindow.Address, false) is { } found
                    && FindWorkspace(movedWindow.Workspace, movedWindow.Monitor, false) is { } destination)
                    MoveWindow(found, destination);
                break;
            case WinlandProtocol.WindowTitleChanged titled:
                FindWindow(titled.Address, true)?.SetTitle(titled.Title);
                break;
            case WinlandProtocol.WindowActivated activated:
                SetActiveWindow(FindWindow(activated.Address, true));
                break;
            case WinlandProtocol.WindowUrgent urgent:
                FindWindow(urgent.Address, true)?.SetUrgent(true);
                break;
        }
    }

    private void RefreshMonitors(bool canCreate)
    {
        Ask(_monitorQuery, "j/monitors", canCreate, ApplyMonitors);
    }

    private void ApplyMonitors(JsonElement json, bool canCreate)
    {
        _monitorsRequested = true;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in json.EnumerateArray())
        {
            var name = Str(entry, "name");
            var monitor = FindMonitor(name, false);
            if (monitor is null)
            {
                if (!canCreate)
                    continue;
                monitor = FindMonitor(name, true)!;
            }

            names.Add(name);
            monitor.UpdateInitial(Int(entry, "id") ?? -1, name, Str(entry, "description"));
            monitor.UpdateGeometry(Int(entry, "x") ?? 0, Int(entry, "y") ?? 0, Int(entry, "width") ?? 0,
                Int(entry, "height") ?? 0, Num(entry, "scale"));
            var active = Obj(entry, "activeWorkspace");
            var activeName = Str(active, "name");
            if (activeName.Length > 0 && monitor.ActiveWorkspace?.Name != activeName)
                monitor.SetActiveWorkspace(FindWorkspace(activeName, name, true, Int(active, "id")));
            monitor.SetLastIpcObject(entry);
            if (Bool(entry, "focused"))
                SetFocusedMonitor(monitor);
        }

        foreach (var monitor in _monitorList.Where(m => !names.Contains(m.Name)).ToArray())
            RemoveMonitor(monitor);
    }

    private void RefreshWorkspaces(bool canCreate)
    {
        Ask(_workspaceQuery, "j/workspaces", canCreate, ApplyWorkspaces);
    }

    private void ApplyWorkspaces(JsonElement json, bool canCreate)
    {
        var entries = json.EnumerateArray().ToList();
        var unnumbered = UnnumberedIds(entries.Select(e => Str(e, "name")));
        var seen = new HashSet<WinlandWorkspace>();
        foreach (var entry in entries)
        {
            var name = Str(entry, "name");
            var monitorName = Str(entry, "monitor");
            var id = Int(entry, "id") ?? unnumbered.GetValueOrDefault(name, IdFromName(name));
            var workspace = FindWorkspace(name, monitorName, false);
            if (workspace is null)
            {
                if (!canCreate)
                    continue;
                workspace = FindWorkspace(name, monitorName, true, id)!;
            }

            workspace.SetId(id);
            if (monitorName.Length > 0)
                workspace.SetMonitor(FindMonitor(monitorName, true, Int(entry, "monitorID") ?? -1));
            workspace.SetHasFullscreen(Bool(entry, "hasfullscreen"));
            workspace.SetLastIpcObject(entry);
            seen.Add(workspace);
        }

        if (canCreate)
            foreach (var workspace in _workspaceList.Where(w => !seen.Contains(w)).ToArray())
                RemoveWorkspace(workspace);
    }

    private WinlandMonitor? FindMonitor(string name, bool create, int id = -1)
    {
        var monitor = _monitorList.FirstOrDefault(m => m.Name == name);
        if (monitor is not null || !create)
            return monitor;
        monitor = new WinlandMonitor(this);
        monitor.UpdateInitial(id, name, "");
        _monitorList.Add(monitor);
        return monitor;
    }

    private WinlandWorkspace? FindWorkspace(string name, string monitorName, bool create, int? id = null)
    {
        if (name.Length == 0)
            return null;
        var named = _workspaceList.Where(w => w.Name == name).ToList();
        var workspace = IsSpecial(name) ? named.FirstOrDefault()
            : monitorName.Length > 0 ? named.FirstOrDefault(w => w.Monitor?.Name == monitorName) ??
                                       named.FirstOrDefault(w => w.Monitor is null)
            : named.OrderByDescending(w => w.Monitor == _focusedMonitor).FirstOrDefault();
        if (workspace is not null || !create)
            return workspace;
        workspace = new WinlandWorkspace(this);
        workspace.UpdateInitial(id ?? IdFromName(name), name);
        if (monitorName.Length > 0)
            workspace.SetMonitor(FindMonitor(monitorName, true));
        _workspaceList.Add(workspace);
        return workspace;
    }

    private WinlandWindow? FindWindow(ulong address, bool create)
    {
        if (address == 0)
            return null;
        var window = _windowList.FirstOrDefault(t => t.RawAddress == address);
        if (window is not null || !create)
            return window;
        window = new WinlandWindow(this);
        window.SetAddress(address);
        _windowList.Add(window);
        return window;
    }

    private static void MoveWindow(WinlandWindow window, WinlandWorkspace workspace)
    {
        if (window.Workspace is { } old && old != workspace)
            old.RemoveWindow(window);
        window.SetWorkspace(workspace);
        workspace.InsertWindow(window);
    }

    private void RemoveWindow(WinlandWindow window)
    {
        if (_activeWindow == window)
            SetActiveWindow(null);
        window.Workspace?.RemoveWindow(window);
        _windowList.Remove(window);
    }

    private void RemoveWorkspace(WinlandWorkspace workspace)
    {
        _workspaceList.Remove(workspace);
        foreach (var monitor in _monitorList.Where(m => m.ActiveWorkspace == workspace))
            monitor.SetActiveWorkspace(null);
        foreach (var window in _windowList.Where(t => t.Workspace == workspace))
            window.SetWorkspace(null);
    }

    private void RemoveMonitor(WinlandMonitor monitor)
    {
        _monitorList.Remove(monitor);
        if (_focusedMonitor == monitor)
            SetFocusedMonitor(null);
        foreach (var workspace in _workspaceList.Where(w => w.Monitor == monitor))
            workspace.SetMonitor(null);
    }

    private void Recompute()
    {
        Monitors.Mirror(_monitorList.OrderBy(m => m.Id));
        Workspaces.Mirror(_workspaceList.OrderBy(w => w.Id).ThenBy(w => w.Monitor?.Id ?? int.MaxValue));
        Windows.Mirror(_windowList);

        var focused = _focusedMonitor?.ActiveWorkspace;
        foreach (var monitor in _monitorList)
            monitor.SetFocused(monitor == _focusedMonitor);
        foreach (var workspace in _workspaceList)
            workspace.SetDerived(workspace.Monitor?.ActiveWorkspace == workspace, workspace == focused);
        foreach (var window in _windowList)
        {
            var activated = window == _activeWindow;
            window.SetDerived(activated, window.Workspace?.Monitor);
            if (activated)
                window.SetUrgent(false);
        }

        foreach (var workspace in _workspaceList)
        {
            var urgent = workspace.Windows.Any(t => t.Urgent);
            if (urgent && workspace.Focused)
            {
                foreach (var window in workspace.Windows)
                    window.SetUrgent(false);
                urgent = false;
            }

            workspace.SetUrgent(urgent);
        }

        Set(ref _focusedWorkspace, focused, nameof(FocusedWorkspace));
    }

    private void SetFocusedMonitor(WinlandMonitor? monitor)
    {
        Set(ref _focusedMonitor, monitor, nameof(FocusedMonitor));
    }

    private void SetActiveWindow(WinlandWindow? window)
    {
        Set(ref _activeWindow, window, nameof(ActiveWindow));
    }

    void IDisposable.Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Attach(null);
        _listening.Dispose();
    }

    private sealed class QueryState
    {
        public bool Reading { get; set; }

        public bool? Again { get; set; }
    }
}
