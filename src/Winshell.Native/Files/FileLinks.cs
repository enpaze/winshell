using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;

namespace Winshell.Native;

internal static unsafe class FileLinks
{
    internal static bool TryReparsePoint(string path, out bool reparse)
    {
        reparse = false;
        var handle = PInvoke.FindFirstFile(path, out var data);
        if ((nint)handle.Value == -1)
            return false;
        try
        {
            reparse = (data.dwFileAttributes & (uint)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_REPARSE_POINT) != 0;
            return true;
        }
        finally
        {
            PInvoke.FindClose(handle);
        }
    }

    internal static int? Count(SafeFileHandle file)
    {
        return PInvoke.GetFileInformationByHandle(new HANDLE(file.DangerousGetHandle()), out var info)
            ? (int)info.nNumberOfLinks
            : null;
    }
}
