using Windows.Win32;
using Windows.Win32.Foundation;

namespace Winshell.Native;

internal static class ShellHookWindow
{
    internal const nint Flash = (nint)(PInvoke.HSHELL_REDRAW | PInvoke.HSHELL_HIGHBIT);

    private const string ClassName = "WinshellShellHook";

    private static NativeWindow? _window;
    private static uint _message;
    private static Action<nint, nint>? _received;

    internal static bool Start(Action<nint, nint> received)
    {
        _received = received;
        _message = PInvoke.RegisterWindowMessage("SHELLHOOK");
        _window = NativeWindow.TryCreate(ClassName, 0, 0, OnMessage, null, true);
        if (_window is not null && _message != 0 && PInvoke.RegisterShellHookWindow((HWND)_window.Handle))
            return true;
        _window?.Dispose();
        _window = null;
        return false;
    }

    private static nint? OnMessage(WindowMessage message)
    {
        if (message.Id != _message || _message == 0)
            return null;
        _received?.Invoke(message.WParam, message.LParam);
        return 0;
    }
}
