using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

internal static unsafe class TaskbarWindows
{
    internal const string PrimaryClass = "Shell_TrayWnd";

    internal const string SecondaryClass = "Shell_SecondaryTrayWnd";

    internal static readonly uint Created = PInvoke.RegisterWindowMessage("TaskbarCreated");

    internal static IDisposable? Watch(Action changed, Action<Exception> failed)
    {
        var window = NativeWindow.TryCreate("WinshellTaskbarWatch", (uint)WINDOW_STYLE.WS_POPUP,
            (uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW,
            message =>
            {
                if (message.Id != Created && message.Id != PInvoke.WM_DISPLAYCHANGE)
                    return null;
                changed();
                return 0;
            }, failed, false);
        if (window is null)
            return null;
        PInvoke.ChangeWindowMessageFilterEx((HWND)window.Handle, Created, WINDOW_MESSAGE_FILTER_ACTION.MSGFLT_ALLOW,
            null);
        return window;
    }
}
