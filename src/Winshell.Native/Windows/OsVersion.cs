using Windows.Wdk;
using Windows.Win32.System.SystemInformation;

namespace Winshell.Native;

internal static class OsVersion
{
    internal static unsafe uint? Build()
    {
        var info = new OSVERSIONINFOW { dwOSVersionInfoSize = (uint)sizeof(OSVERSIONINFOW) };
        return PInvoke.RtlGetVersion(ref info).Value < 0 ? null : info.dwBuildNumber;
    }
}
