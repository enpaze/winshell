using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

internal static unsafe class GuiThread
{
    internal static nint MenuOwner(uint thread)
    {
        var info = new GUITHREADINFO { cbSize = (uint)sizeof(GUITHREADINFO) };
        return PInvoke.GetGUIThreadInfo(thread, ref info) ? info.hwndMenuOwner : 0;
    }
}
