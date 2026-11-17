using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell.Tray;

internal sealed class TrayBackend(IEventLoop loop, Log log, TrayNames names) : ITrayBackend
{
    private readonly Mutex _owner = new(false, names.Owner);
    private readonly List<WinEventHook> _eventHooks = [];
    private bool _owned;
    private TrayWindow? _window;
    private Func<NotifyIconCall, bool>? _apply;
    private IDisposable? _settingsWatch;

    public event Action? TaskbarCreated;

    public event Action? ExplorerInFront;

    public event Action? SettingsChanged;

    public nint Window => _window?.Handle ?? 0;

    public nint Explorer
    {
        get
        {
            nint hwnd = 0;
            while ((hwnd = PInvoke.FindWindowEx(HWND.Null, (HWND)hwnd, names.Explorer, null)) != 0)
                if (hwnd != Window)
                    return hwnd;
            return 0;
        }
    }

    public (int X, int Y) Cursor
    {
        get
        {
            PInvoke.GetCursorPos(out System.Drawing.Point p);
            return (p.X, p.Y);
        }
    }

    public bool TryOwn()
    {
        try
        {
            _owned = _owned || _owner.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            _owned = true;
        }

        return _owned;
    }

    public bool Open(Func<NotifyIconCall, bool> apply)
    {
        _apply = apply;
        _window = TrayWindow.Create(names.Window, OnCopyData, OnMessage, Failed);
        if (_window is null)
            return false;
        _settingsWatch = TrayIconSettings.Watch(loop, () => SettingsChanged?.Invoke());
        foreach (var eventType in new[] { PInvoke.EVENT_SYSTEM_FOREGROUND, PInvoke.EVENT_OBJECT_REORDER })
            if (WinEventHook.Start(eventType, OnOrderChanged, Failed) is { } hook)
                _eventHooks.Add(hook);
            else
                log.Warn("tray", "Could not watch the taskbar's order");
        return true;
    }

    public void Close()
    {
        _settingsWatch?.Dispose();
        _settingsWatch = null;
        foreach (var hook in _eventHooks)
            hook.Dispose();
        _eventHooks.Clear();
        _window?.Dispose();
        _window = null;
        if (_owned)
            _owner.ReleaseMutex();
        _owned = false;
    }

    public void Dispose()
    {
        Close();
        _owner.Dispose();
    }

    public void Raise()
    {
        var explorer = Explorer;
        if (explorer != 0 && PInvoke.GetWindowRect((HWND)explorer, out var r))
            PInvoke.SetWindowPos((HWND)Window, HWND.HWND_TOPMOST, r.left, r.top, r.right - r.left, r.bottom - r.top,
                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        else
            PInvoke.SetWindowPos((HWND)Window, HWND.HWND_TOPMOST, 0, 0, 0, 0,
                (SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
                       SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE));
    }

    public bool Lower()
    {
        var explorer = Explorer;
        return Window != 0 && explorer != 0 && PInvoke.SetWindowPos((HWND)Window, (HWND)explorer, 0, 0, 0, 0,
            (SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
                   SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE));
    }

    public void Broadcast()
    {
        TrayApp.AskForIcons();
    }

    public IReadOnlyList<TrayIconSetting> ReadSettings()
    {
        return TrayIconSettings.Read();
    }

    public (PixelImage? Image, IconResource? Resource) ReadIcon(nint icon)
    {
        return (IconImage.FromHandle(icon), IconImage.ResourceOf(icon));
    }

    public PixelImage? DrawIcon(IconResource resource, int size)
    {
        return IconImage.FromResource(resource, size);
    }

    public uint ProcessOf(nint window)
    {
        PInvoke.GetWindowThreadProcessId((HWND)window, out var pid);
        return pid;
    }

    public string ProgramOf(nint window)
    {
        return ProcessImage.TryRead(ProcessOf(window)) ?? "";
    }

    public uint? IntegrityOf(uint processId)
    {
        return ProcessIntegrity.TryRead(processId);
    }

    public void MoveCursor((int X, int Y) point)
    {
        PointerInput.MoveTo(new POINT { X = point.X, Y = point.Y }, asMouse: false);
    }

    public void LetAppTakeForeground(nint window, uint processId)
    {
        if (TrayApp.AllowForeground(processId))
            return;
        Focus.Take(window);
        TrayApp.AllowForeground(processId);
    }

    public void Notify(nint window, uint message, nint wParam, nint lParam)
    {
        TrayApp.Notify(window, message, wParam, lParam);
    }

    private void OnOrderChanged(uint eventType, nint hwnd, int idObject, int idChild)
    {
        if (idChild != (int)PInvoke.CHILDID_SELF || Window == 0)
            return;
        var explorer = Explorer;
        var desktopOrder = eventType == PInvoke.EVENT_OBJECT_REORDER && idObject == (int)OBJECT_IDENTIFIER.OBJID_CLIENT &&
                           hwnd == WindowTree.Desktop;
        if (explorer == 0 || (!desktopOrder && (hwnd != explorer || idObject != (int)OBJECT_IDENTIFIER.OBJID_WINDOW)))
            return;
        for (var above = PInvoke.GetWindow((HWND)explorer, GET_WINDOW_CMD.GW_HWNDPREV);
             above != 0;
             above = PInvoke.GetWindow(above, GET_WINDOW_CMD.GW_HWNDPREV))
            if (above == Window)
                return;
        ExplorerInFront?.Invoke();
    }

    private void Failed(Exception e)
    {
        log.Warn("tray", e.Message);
    }

    private nint OnCopyData(CopyData data)
    {
        var answer = 1;
        if (data.Kind == TrayProtocol.NotifyIconData && TrayProtocol.Read(data.Payload) is { } call)
            answer = _apply!(call) ? 1 : 0;
        var explorer = Explorer;
        if (explorer == 0)
            return answer;
        var icon = data.Kind == TrayProtocol.NotifyIconData;
        var result = data.Forward(explorer, icon ? 200u : 2000u);
        return icon ? answer : result;
    }

    private bool OnMessage(uint msg, nint wParam, nint lParam)
    {
        if (msg == TaskbarWindows.Created)
        {
            TaskbarCreated?.Invoke();
            return true;
        }

        if (msg == PInvoke.WM_SYSCOMMAND || (msg == PInvoke.WM_COMMAND && lParam == 0))
        {
            var explorer = Explorer;
            if (explorer != 0)
                PInvoke.PostMessage((HWND)explorer, msg, (WPARAM)(nuint)wParam, lParam);
            return true;
        }

        return false;
    }
}
