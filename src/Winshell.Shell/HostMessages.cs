using Winshell.Native;

namespace Winshell;

internal sealed class HostMessages(HostWindow host, ShellPlatform platform)
{
    internal void Press()
    {
        if (WindowKindDescription.Of(host.Kind).DismissesMenusOnPress)
            NativeMenu.Dismiss();
    }

    public nint? OnMessage(WindowMessage message)
    {
        return OnWindowMessage(message) ?? host.Placer.OnMessage(message);
    }

    private nint? OnWindowMessage(WindowMessage message)
    {
        var placement = host.Placement;
        var (msg, wParam) = (message.Id, message.WParam);
        switch (msg)
        {
            case PInvoke.WM_SIZE:
            case PInvoke.WM_MOVE:
                if (host.Handle != 0)
                    placement.UpdateSize();
                return null;
            case PInvoke.WM_DPICHANGED:
                {
                    if (!placement.Placing)
                        placement.Scale = DesktopSpace.Scale((uint)Win32.HighWord(wParam));
                    host.Placer.OnDpiChanged(message.SuggestedRect, placement.Placing);
                    placement.UpdateSize();
                    platform.ScreensMayHaveChanged();
                    return 0;
                }
            case PInvoke.WM_MOUSEACTIVATE:
                Press();
                return host.Focus.ActivatesOnClick ? null : (nint)PInvoke.MA_NOACTIVATE;
            case PInvoke.WM_ACTIVATE:
                host.Client.OnActiveChanged(Win32.LowWord(wParam) != 0);
                return null;
            case PInvoke.WM_SETFOCUS:
                host.Presentation.FocusContent();
                return null;
            case PInvoke.WM_CLOSE:
                host.Client.OnClosed();
                placement.SetVisible(false);
                return 0;
            case PInvoke.WM_DESTROY:
                host.NoteExternalDestroy();
                return null;
            case PInvoke.WM_ERASEBKGND:
                return 1;
            default:
                return null;
        }
    }
}
