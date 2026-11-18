using Winshell.Native;

namespace Winshell;

internal sealed class HostMessages(HostWindow host, ShellPlatform platform)
{
    internal void DismissNativeMenus()
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
        var place = host.Place;
        var (msg, wParam) = (message.Id, message.WParam);
        switch (msg)
        {
            case PInvoke.WM_SIZE:
            case PInvoke.WM_MOVE:
                if (host.Handle != 0)
                    place.UpdateSize();
                return null;
            case PInvoke.WM_DPICHANGED:
                {
                    if (!place.Placing)
                        place.Scale = DesktopSpace.Scale((uint)Win32.HighWord(wParam));
                    host.Placer.OnDpiChanged(message.SuggestedRect, place.Placing);
                    place.UpdateSize();
                    platform.ScreensMayHaveChanged();
                    return 0;
                }
            case PInvoke.WM_MOUSEACTIVATE:
                DismissNativeMenus();
                return host.Focus.ActivatesOnClick ? null : (nint)PInvoke.MA_NOACTIVATE;
            case PInvoke.WM_ACTIVATE:
                host.Client.OnActiveChanged(Win32.LowWord(wParam) != 0);
                return null;
            case PInvoke.WM_SETFOCUS:
                host.Presentation.FocusContent();
                return null;
            case PInvoke.WM_CLOSE:
                host.Client.OnClosed();
                place.SetVisible(false);
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
