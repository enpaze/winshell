using System.Runtime.InteropServices;
using Windows.Win32;

namespace Winshell.Native;

internal static class KnownFolder
{
    internal static unsafe string? Find(Guid id)
    {
        var hr = PInvoke.SHGetKnownFolderPath(id, default, default, out var path);
        try
        {
            return hr.Succeeded ? path.ToString() : null;
        }
        finally
        {
            Marshal.FreeCoTaskMem((nint)path.Value);
        }
    }
}
