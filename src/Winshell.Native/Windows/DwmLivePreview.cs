using System.Runtime.InteropServices;

namespace Winshell.Native;

internal static partial class DwmLivePreview
{
    private const uint PeekOneWindow = 1;

    internal static bool Set(bool on, nint window, nint above)
    {
        try
        {
            return DwmpActivateLivePreview(on, window, above, PeekOneWindow, 0) == 0;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [LibraryImport("dwmapi.dll", EntryPoint = "#113")]
    private static partial int DwmpActivateLivePreview([MarshalAs(UnmanagedType.Bool)] bool on, nint window, nint above,
        uint type, nuint reserved);
}
