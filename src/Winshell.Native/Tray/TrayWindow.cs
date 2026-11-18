using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.DataExchange;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

internal readonly unsafe ref struct CopyData
{
    private readonly nint _sender;
    private readonly COPYDATASTRUCT* _data;

    internal CopyData(nint sender, COPYDATASTRUCT* data)
    {
        _sender = sender;
        _data = data;
    }

    internal nuint Kind => _data->dwData;

    internal ReadOnlySpan<byte> Payload =>
        _data->lpData == null ? default : new ReadOnlySpan<byte>(_data->lpData, (int)_data->cbData);

    internal nint Forward(nint window, uint timeoutMilliseconds)
    {
        PInvoke.SendMessageTimeout((HWND)window, PInvoke.WM_COPYDATA, (nuint)_sender, (nint)_data,
            SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG, timeoutMilliseconds, out var result);
        return (nint)result;
    }
}

internal delegate nint CopyDataHandler(CopyData data);

internal sealed unsafe class TrayWindow : IDisposable
{
    private readonly NativeWindow _window;

    private TrayWindow(NativeWindow window)
    {
        _window = window;
    }

    internal nint Handle => _window.Handle;

    internal static TrayWindow? Create(string className, CopyDataHandler copyData, Func<uint, nint, nint, bool> message,
        Action<Exception> failed)
    {
        var window = NativeWindow.TryCreate(className, (uint)WINDOW_STYLE.WS_POPUP,
            (uint)(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_NOACTIVATE |
                   WINDOW_EX_STYLE.WS_EX_LAYERED | WINDOW_EX_STYLE.WS_EX_TRANSPARENT), OnMessage(copyData, message),
            failed, false);
        if (window is null)
            return null;
        PInvoke.SetLayeredWindowAttributes((HWND)window.Handle, default, 0, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
        foreach (var allowed in new[] { TaskbarWindows.Created, PInvoke.WM_COPYDATA })
            PInvoke.ChangeWindowMessageFilterEx((HWND)window.Handle, allowed, WINDOW_MESSAGE_FILTER_ACTION.MSGFLT_ALLOW,
                null);
        return new TrayWindow(window);
    }

    public void Dispose()
    {
        _window.Dispose();
    }

    private static WindowMessageHandler OnMessage(CopyDataHandler copyData, Func<uint, nint, nint, bool> message)
    {
        return received =>
        {
            if (received.Id == PInvoke.WM_COPYDATA)
                return received.LParam == 0
                    ? 0
                    : copyData(new CopyData(received.WParam, (COPYDATASTRUCT*)received.LParam));
            return message(received.Id, received.WParam, received.LParam) ? 0 : null;
        };
    }
}
