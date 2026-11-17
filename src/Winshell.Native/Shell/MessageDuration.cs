using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

internal static unsafe class MessageDuration
{
    internal static uint? Seconds()
    {
        uint seconds = 0;
        return PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETMESSAGEDURATION, 0, &seconds,
            default)
            ? seconds
            : null;
    }
}
