using System.Globalization;
using System.Text.Json;
using Winshell.Windows;

namespace Winshell.Winland;

public sealed record WinlandEvent(string Name, string Data)
{
    public IReadOnlyList<string> Parse(int argumentCount)
    {
        return WinlandProtocol.ParseArgs(Data, argumentCount);
    }
}

public sealed class WinlandMonitor : ObservableObject
{
    private readonly WinlandService _service;
    private int _id = -1;
    private string _name = "";
    private string _description = "";
    private int _x, _y, _width, _height;
    private double _scale = 1;
    private bool _focused;
    private WinlandWorkspace? _activeWorkspace;
    private JsonElement? _lastIpcObject;

    internal WinlandMonitor(WinlandService service)
    {
        _service = service;
    }

    public int Id => Read(_id);

    public string Name => Read(_name);

    public string Description => Read(_description);

    public int X => Read(_x);

    public int Y => Read(_y);

    public int Width => Read(_width);

    public int Height => Read(_height);

    public double Scale => Read(_scale);

    public bool Focused => Read(_focused);

    public WinlandWorkspace? ActiveWorkspace => Read(_activeWorkspace);

    public JsonElement? LastIpcObject => Read(_lastIpcObject);

    public void ActivateWorkspace(int number)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        ActivateWorkspace(number.ToString(CultureInfo.InvariantCulture));
    }

    internal void ActivateWorkspace(string name)
    {
        if (this != _service.FocusedMonitor)
            _service.Send(
                $"[[BATCH]]dispatch focusmonitor {(_id >= 0 ? _id.ToString(CultureInfo.InvariantCulture) : _name)};dispatch workspace {name}");
        else
            _service.Dispatch("workspace " + name);
    }

    internal void UpdateInitial(int id, string name, string description)
    {
        Set(ref _id, id, nameof(Id));
        Set(ref _name, name, nameof(Name));
        Set(ref _description, description, nameof(Description));
    }

    internal void UpdateGeometry(int x, int y, int width, int height, double scale)
    {
        Set(ref _x, x, nameof(X));
        Set(ref _y, y, nameof(Y));
        Set(ref _width, width, nameof(Width));
        Set(ref _height, height, nameof(Height));
        Set(ref _scale, scale, nameof(Scale));
    }

    internal void SetLastIpcObject(JsonElement value)
    {
        _lastIpcObject = value;
        Changed(nameof(LastIpcObject));
    }

    internal void SetActiveWorkspace(WinlandWorkspace? workspace)
    {
        workspace?.SetMonitor(this);
        Set(ref _activeWorkspace, workspace, nameof(ActiveWorkspace));
    }

    internal void SetFocused(bool focused)
    {
        Set(ref _focused, focused, nameof(Focused));
    }
}

public sealed class WinlandWorkspace : ObservableObject
{
    private readonly WinlandService _service;
    private int _id = -1;
    private string _name = "";
    private WinlandMonitor? _monitor;
    private bool _active, _focused, _urgent, _hasFullscreen;
    private JsonElement? _lastIpcObject;

    internal WinlandWorkspace(WinlandService service)
    {
        _service = service;
    }

    public int Id => Read(_id);

    public string Name => Read(_name);

    public WinlandMonitor? Monitor => Read(_monitor);

    public bool Active => Read(_active);

    public bool Focused => Read(_focused);

    public bool Urgent => Read(_urgent);

    public bool HasFullscreen => Read(_hasFullscreen);

    public ObservableList<WinlandWindow> Windows { get => Read(field); } = [];

    public IReadOnlyList<string> AppIcons =>
        Windows.Select(window => window.Window?.Icon ?? "").Where(icon => icon.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public JsonElement? LastIpcObject => Read(_lastIpcObject);

    private bool IsSpecial => WinlandProtocol.IsSpecial(_name);

    public void Activate()
    {
        if (!IsSpecial && _monitor is { Id: >= 0 } monitor)
            monitor.ActivateWorkspace(_name);
        else
            _service.Dispatch("workspace " + _name);
    }

    internal void UpdateInitial(int id, string name)
    {
        Set(ref _id, id, nameof(Id));
        Set(ref _name, name, nameof(Name));
    }

    internal void SetId(int id)
    {
        Set(ref _id, id, nameof(Id));
    }

    internal void SetMonitor(WinlandMonitor? monitor)
    {
        Set(ref _monitor, monitor, nameof(Monitor));
    }

    internal void SetHasFullscreen(bool value)
    {
        Set(ref _hasFullscreen, value, nameof(HasFullscreen));
    }

    internal void SetLastIpcObject(JsonElement value)
    {
        _lastIpcObject = value;
        Changed(nameof(LastIpcObject));
    }

    internal void SetDerived(bool active, bool focused)
    {
        Set(ref _active, active, nameof(Active));
        Set(ref _focused, focused, nameof(Focused));
    }

    internal void SetUrgent(bool urgent)
    {
        Set(ref _urgent, urgent, nameof(Urgent));
    }

    internal void InsertWindow(WinlandWindow window)
    {
        if (!Windows.Contains(window))
            Windows.Add(window);
    }

    internal void RemoveWindow(WinlandWindow window)
    {
        Windows.Remove(window);
    }
}

public sealed class WinlandWindow : ObservableObject
{
    private readonly WinlandService _service;
    private ulong _address;
    private string _title = "";
    private WinlandWorkspace? _workspace;
    private WinlandMonitor? _monitor;
    private bool _activated, _urgent;
    private AppWindow? _seen;
    private JsonElement? _lastIpcObject;

    internal WinlandWindow(WinlandService service)
    {
        _service = service;
    }

    public string Address => Read(_address) == 0 ? "" : _address.ToString("x", CultureInfo.InvariantCulture);

    public nint Handle => (nint)Read(_address);

    public AppWindow? Window => Read(_service).FindWindow(Handle);

    public string Title => Read(_title);

    public WinlandWorkspace? Workspace => Read(_workspace);

    public WinlandMonitor? Monitor => Read(_monitor);

    public bool Activated => Read(_activated);

    public bool Urgent => Read(_urgent);

    public JsonElement? LastIpcObject => Read(_lastIpcObject);

    internal ulong RawAddress => _address;

    internal void SetAddress(ulong address)
    {
        if (_address == address)
            return;
        _address = address;
        Changed(nameof(Address));
        Changed(nameof(Handle));
        NotifyWindow();
    }

    internal void NotifyWindow()
    {
        var window = Window;
        if (ReferenceEquals(_seen, window))
            return;
        _seen = window;
        Changed(nameof(Window));
    }

    internal void SetTitle(string title)
    {
        Set(ref _title, title, nameof(Title));
    }

    internal void SetWorkspace(WinlandWorkspace? workspace)
    {
        Set(ref _workspace, workspace, nameof(Workspace));
    }

    internal void SetUrgent(bool urgent)
    {
        Set(ref _urgent, urgent, nameof(Urgent));
    }

    internal void SetLastIpcObject(JsonElement value)
    {
        _lastIpcObject = value;
        Changed(nameof(LastIpcObject));
    }

    internal void SetDerived(bool activated, WinlandMonitor? monitor)
    {
        Set(ref _activated, activated, nameof(Activated));
        Set(ref _monitor, monitor, nameof(Monitor));
    }
}
