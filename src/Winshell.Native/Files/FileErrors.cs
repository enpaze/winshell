using Windows.Win32.Foundation;

namespace Winshell.Native;

internal static class FileErrors
{
    internal static bool IsLock(Exception exception)
    {
        var code = (WIN32_ERROR)(exception.HResult & 0xFFFF);
        return code is WIN32_ERROR.ERROR_ACCESS_DENIED or WIN32_ERROR.ERROR_SHARING_VIOLATION
            or WIN32_ERROR.ERROR_LOCK_VIOLATION;
    }
}
