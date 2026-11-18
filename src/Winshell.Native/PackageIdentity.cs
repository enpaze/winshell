using Windows.Win32;
using Windows.Win32.Foundation;

namespace Winshell.Native;

internal static unsafe class PackageIdentity
{
    private const int MaxFamilyName = 128;

    internal static bool Exists()
    {
        uint length = 0;
        return PInvoke.GetCurrentPackageFullName(&length) != WIN32_ERROR.APPMODEL_ERROR_NO_PACKAGE;
    }

    internal static string FamilyName(uint processId)
    {
        return ProcessHandle.ReadText(processId, MaxFamilyName,
            (process, length, text) => PInvoke.GetPackageFamilyName(process, length, text));
    }
}
