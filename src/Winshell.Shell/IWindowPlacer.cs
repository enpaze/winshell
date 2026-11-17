using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell;

internal interface IWindowPlacer
{
    (uint Style, uint ExStyle) Styles => ((uint)WINDOW_STYLE.WS_POPUP,
        (uint)(WINDOW_EX_STYLE.WS_EX_NOREDIRECTIONBITMAP | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |
               WINDOW_EX_STYLE.WS_EX_TOPMOST));

    void Show()
    {
    }

    void Hide()
    {
    }

    void Release()
    {
    }

    void OnDpiChanged(RECT suggested, bool placing);

    void OnWorkAreaChanged()
    {
    }

    nint? OnMessage(WindowMessage message)
    {
        return null;
    }
}
