using Windows.Win32;
using Windows.Win32.Foundation;

namespace Winshell.Native;

/// <summary>Whether this process runs with an MSIX package identity, and a process's package family name.</summary>
internal static unsafe class PackageIdentity
{
    /// <summary>Characters, including the terminating null, that a package family name can hold.</summary>
    private const int MaxFamilyName = 128;

    /// <summary>Asks for the length of the package's full name, which fails with no identity.</summary>
    internal static bool Exists()
    {
        uint length = 0;
        return PInvoke.GetCurrentPackageFullName(&length) != WIN32_ERROR.APPMODEL_ERROR_NO_PACKAGE;
    }

    /// <summary>
    /// The package family name of <paramref name="processId"/>, or empty when that process has no package identity
    /// or cannot be opened. Zero is empty.
    /// </summary>
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
                // The length counts the terminating null, which must fit too.
                return new TextBuffer.Answer(code, length > (uint)buffer.Length ? -1 : (int)length - 1);
            }
        }) ?? "";
    }
}
