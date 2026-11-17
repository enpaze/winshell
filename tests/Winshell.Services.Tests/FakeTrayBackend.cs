using Winshell.Native;
using Winshell.Tray;

namespace Winshell.Services.Tests;

internal sealed class FakeTrayBackend : ITrayBackend
{
    private Func<NotifyIconCall, bool>? _apply;

    public event Action? TaskbarCreated;

    public event Action? ExplorerInFront;

    public event Action? SettingsChanged;

    public List<string> Calls { get; } = [];

    public List<(nint Window, uint Message, nint WParam, nint LParam)> Notified { get; } = [];

    public bool Taken { get; set; }

    public bool IsOpen => _apply is not null;

    public nint Explorer { get; set; } = 0x500;

    public (int X, int Y) Cursor { get; init; }

    public List<TrayIconSetting> Settings { get; } = [];

    public Dictionary<nint, uint> Windows { get; } = [];

    public Dictionary<nint, string> Programs { get; } = [];

    public Dictionary<uint, uint> Integrities { get; } = [];

    public Dictionary<nint, PixelImage> Images { get; } = [];

    public Dictionary<nint, IconResource> Resources { get; } = [];

    public Dictionary<(IconResource Resource, int Size), PixelImage> Drawn { get; } = [];

    public void Send(NotifyIconCall call)
    {
        (_apply ?? throw new InvalidOperationException("The tray has not opened its window."))(call);
    }

    public void SignalTaskbarCreated()
    {
        TaskbarCreated?.Invoke();
    }

    public void SignalExplorerInFront()
    {
        ExplorerInFront?.Invoke();
    }

    public void SignalSettingsChanged()
    {
        SettingsChanged?.Invoke();
    }

    public bool TryOwn()
    {
        return !Taken;
    }

    public bool Open(Func<NotifyIconCall, bool> apply)
    {
        Calls.Add("open");
        _apply = apply;
        return true;
    }

    public void Close()
    {
        Calls.Add("close");
        _apply = null;
    }

    public void Dispose()
    {
        Calls.Add("dispose");
    }

    public void Raise()
    {
        Calls.Add("raise");
    }

    public bool Lower()
    {
        Calls.Add("lower");
        return IsOpen && Explorer != 0;
    }

    public void Broadcast()
    {
        Calls.Add("broadcast");
    }

    public IReadOnlyList<TrayIconSetting> ReadSettings()
    {
        return [.. Settings];
    }

    public (PixelImage? Image, IconResource? Resource) ReadIcon(nint icon)
    {
        return (Images.GetValueOrDefault(icon), Resources.GetValueOrDefault(icon));
    }

    public PixelImage? DrawIcon(IconResource resource, int size)
    {
        return Drawn.GetValueOrDefault((resource, size));
    }

    public uint ProcessOf(nint window)
    {
        return Windows.GetValueOrDefault(window);
    }

    public string ProgramOf(nint window)
    {
        return Programs.GetValueOrDefault(window, "");
    }

    public uint? IntegrityOf(uint processId)
    {
        return Integrities.GetValueOrDefault(processId, ProcessIntegrity.Medium);
    }

    public void MoveCursor((int X, int Y) point)
    {
        Calls.Add($"move {point.X},{point.Y}");
    }

    public void LetAppTakeForeground(nint window, uint processId)
    {
        Calls.Add($"foreground {window:x} {processId}");
    }

    public void Notify(nint window, uint message, nint wParam, nint lParam)
    {
        Calls.Add("notify");
        Notified.Add((window, message, wParam, lParam));
    }
}
