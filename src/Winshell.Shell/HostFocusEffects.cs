using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell;

internal sealed class HostFocusEffects(HostWindow host, ShellPlatform platform) : IFocusEffects
{
    public void AllowActivation(bool allow)
    {
        host.Placement.SetExStyle((uint)WINDOW_EX_STYLE.WS_EX_NOACTIVATE, !allow);
    }

    public void Show(bool activate)
    {
        var show = activate ? SHOW_WINDOW_CMD.SW_SHOW : SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE;
        PInvoke.ShowWindow((HWND)host.Handle, show);
        if (!PInvoke.IsWindowVisible((HWND)host.Handle))
            PInvoke.ShowWindow((HWND)host.Handle, show);
    }

    public void TakeForeground()
    {
        PInvoke.SetForegroundWindow((HWND)host.Handle);
    }

    public void TakeKeyboard()
    {
        Focus.Take(host.Handle);
        host.Presentation.FocusContent();
    }

    public void GrabOutsideClicks(bool on)
    {
        platform.OutsideClicks.Grab(host, on);
    }
}
