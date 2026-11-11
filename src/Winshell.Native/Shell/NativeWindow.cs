using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

/// <summary>A window message as it arrived, and what its pointer carries, readable only while it is handled.</summary>
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

    /// <summary>The rectangle a <c>WM_DPICHANGED</c> message suggests, in physical pixels.</summary>
    internal RECT SuggestedRect
    {
        get
        {
            Expect(PInvoke.WM_DPICHANGED);
            return *(RECT*)LParam;
        }
    }

    /// <summary>The track sizes a <c>WM_GETMINMAXINFO</c> message carries, which its handler changes in place.</summary>
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

/// <summary>Answers a window message, or returns null to leave it to Windows.</summary>
internal delegate nint? WindowMessageHandler(WindowMessage message);

/// <summary>
/// The one window procedure in Native. A class is registered once, and each window's messages go to its handler on
/// the thread that made it. The class carries the logo embedded in the program. A message whose handler throws goes
/// to Windows' default handling. <c>WM_NCDESTROY</c> drops the handler, including a window nobody released.
/// </summary>
internal sealed unsafe class NativeWindow
{
    private const int ErrorClassAlreadyExists = 1410;

    private static readonly ConcurrentDictionary<nint, NativeWindow> Live = new();
    private static readonly ConcurrentDictionary<string, byte> Registered = new();

    private readonly WindowMessageHandler _handler;
    private readonly Action<Exception> _failed;

    private NativeWindow(nint handle, WindowMessageHandler handler, Action<Exception> failed)
    {
        Handle = handle;
        _handler = handler;
        _failed = failed;
    }

    /// <summary>The window, or 0 once released or destroyed.</summary>
    internal nint Handle { get; private set; }

    /// <summary>Makes a window of <paramref name="className"/>, registered once in the process. Throws when Windows would not.</summary>
    /// <param name="className">The window class, registered once in the process.</param>
    /// <param name="style">The window style.</param>
    /// <param name="exStyle">The extended window style.</param>
    /// <param name="handler">Answers a message, or returns null to leave it to Windows.</param>
    /// <param name="failed">Gets each exception <paramref name="handler"/> throws.</param>
    internal static NativeWindow Create(string className, uint style, uint exStyle, WindowMessageHandler handler,
        Action<Exception> failed)
    {
        return Make(className, style, exStyle, handler, failed, HWND.Null, 1)
               ?? throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastPInvokeError()}");
    }

    /// <summary>
    /// Makes a window of <paramref name="className"/>, registered once in the process. Null when Windows would not.
    /// <paramref name="messageOnly"/> keeps it off the desktop: only messages sent to it arrive.
    /// </summary>
    /// <param name="className">The window class, registered once in the process.</param>
    /// <param name="style">The window style.</param>
    /// <param name="exStyle">The extended window style.</param>
    /// <param name="handler">Answers a message, or returns null to leave it to Windows.</param>
    /// <param name="failed">Gets each exception <paramref name="handler"/> throws.</param>
    /// <param name="messageOnly">Keeps the window off the desktop, so only messages sent to it arrive.</param>
    internal static NativeWindow? TryCreate(string className, uint style, uint exStyle, WindowMessageHandler handler,
        Action<Exception> failed, bool messageOnly)
    {
        var parent = messageOnly ? HWND.HWND_MESSAGE : HWND.Null;
        return Make(className, style, exStyle, handler, failed, parent, 0);
    }

    /// <summary>
    /// A window of <paramref name="className"/>. <c>CreateWindowEx</c> takes a pointer for its last argument, which
    /// only this assembly can pass, so this passes null.
    /// </summary>
    internal static nint OfClass(string className, string title, uint style, int x, int y, int width, int height,
        nint parent, nint instance, uint exStyle = 0)
    {
        return PInvoke.CreateWindowEx((WINDOW_EX_STYLE)exStyle, className, title, (WINDOW_STYLE)style, x, y, width,
            height, (HWND)parent, default, (HINSTANCE)instance, null);
    }

    private static NativeWindow? Make(string className, uint style, uint exStyle, WindowMessageHandler handler,
        Action<Exception> failed, HWND parent, int size)
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

    /// <summary>
    /// Stops the handler getting messages, then destroys the window when <paramref name="destroy"/> is true. False
    /// leaves it to whoever is destroying it already, as Windows does a window whose owner closes.
    /// </summary>
    internal void Release(bool destroy)
    {
        var hwnd = Handle;
        if (hwnd == 0)
            return;
        Forget(hwnd);
        if (destroy)
            PInvoke.DestroyWindow((HWND)hwnd);
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

    /// <summary>
    /// The logo embedded in the program. The C# compiler stores <c>ApplicationIcon</c> as icon group 32512, so the
    /// icon shows on a window of this class that has a caption.
    /// </summary>
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
                // The last message a window gets, so a window its handler did not release lets go of it here.
                if (msg == PInvoke.WM_NCDESTROY)
                    window.Forget(handle);
            }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }
}
