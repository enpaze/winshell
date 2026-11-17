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
        if (processId == 0)
            return "";
        using var process = ProcessHandle.Open(processId);
        if (!process.IsOpen)
            return "";
        var handle = process.Handle;
        return TextBuffer.Read(0, MaxFamilyName, buffer =>
        {
            var length = (uint)buffer.Length;
            fixed (char* text = buffer)
            {
                var code = PInvoke.GetPackageFamilyName(handle, &length, text);
                if (code != WIN32_ERROR.ERROR_SUCCESS)
                    return new TextBuffer.Answer(code, (int)length);
                return new TextBuffer.Answer(code, length > (uint)buffer.Length ? -1 : (int)length - 1);
            }
        }) ?? "";
    }
}
