using Microsoft.Win32;
using Winshell.Native;

namespace Winshell;

internal sealed class WindowPeek(Func<bool> turnedOn, Func<bool, nint, nint, bool> set)
{
    private nint _window;
    private nint _above;

    public static WindowPeek ForSystem()
    {
        return new WindowPeek(SettingIsOn, DwmLivePreview.Set);
    }

    public void Show(nint window, nint above)
    {
        if (window == _window && above == _above)
            return;
        End();
        if (window != 0 && turnedOn() && set(true, window, above))
            (_window, _above) = (window, above);
    }

    public void End()
    {
        if (_window == 0)
            return;
        set(false, _window, _above);
        _window = 0;
        _above = 0;
    }

    private static bool SettingIsOn()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
        return key?.GetValue("EnableAeroPeek") is int and not 0;
    }
}
