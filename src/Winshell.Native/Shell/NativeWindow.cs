using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

internal readonly unsafe ref struct WindowMessage
{
    internal WindowMessage(uint id, nint wParam, nint lParam)
    {
        Id = id;
        WParam = wParam;
        LParam = lParam;
    }

    internal uint Id { get; }

    internal nint WParam { get; }

    internal nint LParam { get; }

    internal RECT SuggestedRect
    {
        get
        {
            Expect(PInvoke.WM_DPICHANGED);
            return *(RECT*)LParam;
        }
    }

    internal ref MINMAXINFO MinMaxInfo
    {
        get
        {
            Expect(PInvoke.WM_GETMINMAXINFO);
            return ref *(MINMAXINFO*)LParam;
        }
    }

    private void Expect(uint id)
    {
        if (Id != id || LParam == 0)
            throw new InvalidOperationException($"Message 0x{Id:X4} does not carry what message 0x{id:X4} does.");
    }
}

internal delegate nint? WindowMessageHandler(WindowMessage message);

internal sealed unsafe class NativeWindow : IDisposable
{
    private const int ErrorClassAlreadyExists = 1410;

    private static readonly ConcurrentDictionary<nint, NativeWindow> Live = new();
    private static readonly ConcurrentDictionary<string, byte> Registered = new();

    private readonly WindowMessageHandler _handler;
    private readonly Action<Exception>? _failed;

    private NativeWindow(nint handle, WindowMessageHandler handler, Action<Exception>? failed)
    {
        Handle = handle;
        _handler = handler;
        _failed = failed;
    }

    internal nint Handle { get; private set; }

    internal static NativeWindow Create(string className, uint style, uint exStyle, WindowMessageHandler handler,
        Action<Exception> failed)
    {
        return Make(className, style, exStyle, handler, failed, HWND.Null, 1)
               ?? throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastPInvokeError()}");
    }

    internal static NativeWindow? TryCreate(string className, uint style, uint exStyle, WindowMessageHandler handler,
        Action<Exception>? failed, bool messageOnly)
    {
        var parent = messageOnly ? HWND.HWND_MESSAGE : HWND.Null;
        return Make(className, style, exStyle, handler, failed, parent, 0);
    }

    internal static nint OfClass(string className, string title, uint style, int x, int y, int width, int height,
        nint parent, nint instance, uint exStyle = 0)
    {
        return PInvoke.CreateWindowEx((WINDOW_EX_STYLE)exStyle, className, title, (WINDOW_STYLE)style, x, y, width,
            height, (HWND)parent, default, (HINSTANCE)instance, null);
    }

    private static NativeWindow? Make(string className, uint style, uint exStyle, WindowMessageHandler handler,
        Action<Exception>? failed, HWND parent, int size)
    {
        if (Registered.TryAdd(className, 0))
            Register(className);
        var created = OfClass(className, "", style, 0, 0, size, size, (nint)parent, PInvoke.GetModuleHandle(null),
            exStyle);
        if (created == 0)
            return null;
        var hwnd = created;
        var window = new NativeWindow(hwnd, handler, failed);
        Live[hwnd] = window;
        return window;
    }

    internal void Release(bool destroy)
    {
        var hwnd = Handle;
        if (hwnd == 0)
            return;
        Forget(hwnd);
        if (destroy)
            PInvoke.DestroyWindow((HWND)hwnd);
    }

    public void Dispose()
    {
        Release(true);
    }

    private void Forget(nint hwnd)
    {
        Live.TryRemove(new KeyValuePair<nint, NativeWindow>(hwnd, this));
        if (Handle == hwnd)
            Handle = 0;
    }

    private static void Register(string className)
    {
        fixed (char* name = className)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                style = WNDCLASS_STYLES.CS_DBLCLKS,
                lpfnWndProc = &WndProc,
                hInstance = PInvoke.GetModuleHandle(null),
                hIcon = (HICON)AppIcon(PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXICON),
                    PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYICON)),
                hIconSm = (HICON)AppIcon(PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSMICON),
                    PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSMICON)),
                hCursor = PInvoke.LoadCursor(default, PInvoke.IDC_ARROW),
                lpszClassName = name
            };
            if (PInvoke.RegisterClassEx(in wc) != 0 || Marshal.GetLastPInvokeError() == ErrorClassAlreadyExists)
                return;
        }

        Registered.TryRemove(className, out _);
    }

    private static nint AppIcon(int width, int height)
    {
        return PInvoke.LoadImage(PInvoke.GetModuleHandle(null), PInvoke.IDI_APPLICATION, GDI_IMAGE_TYPE.IMAGE_ICON,
            width, height, default);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        var handle = (nint)hwnd;
        if (Live.TryGetValue(handle, out var window))
            try
            {
                if (Callback.Run(window._failed,
                        () => window._handler(new WindowMessage(msg, (nint)wParam.Value, lParam)), null) is { } result)
                    return (LRESULT)result;
            }
            finally
            {
                if (msg == PInvoke.WM_NCDESTROY)
                    window.Forget(handle);
            }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }
}
