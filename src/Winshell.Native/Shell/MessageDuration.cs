using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

/// <summary>How long Windows shows a notification, as set in Accessibility.</summary>
internal static unsafe class MessageDuration
{
    /// <summary>The duration in seconds, or null when Windows does not say.</summary>
    internal static uint? Seconds()
    {
        uint seconds = 0;
        return PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETMESSAGEDURATION, 0, &seconds,
            default)
            ? seconds
            : null;
    }
}
