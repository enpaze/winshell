using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;
using Windows.Win32.System.Ole;

namespace Winshell.Native;

internal static class ClipboardImage
{
    internal static unsafe void SetDibV5(byte[] dib, nint owner)
    {
        var memory = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, (nuint)dib.Length);
        if (memory.IsNull)
            throw new Win32Exception();
        try
        {
            var pointer = PInvoke.GlobalLock(memory);
            if (pointer is null)
                throw new Win32Exception();
            try
            {
                Marshal.Copy(dib, 0, (nint)pointer, dib.Length);
            }
            finally
            {
                PInvoke.GlobalUnlock(memory);
            }

            if (!PInvoke.OpenClipboard((HWND)owner))
                throw new Win32Exception();
            try
            {
                if (!PInvoke.EmptyClipboard() ||
                    PInvoke.SetClipboardData((uint)CLIPBOARD_FORMAT.CF_DIBV5, (HANDLE)(nint)memory).IsNull)
                    throw new Win32Exception();
                memory = default;
            }
            finally
            {
                PInvoke.CloseClipboard();
            }
        }
        finally
        {
            if (!memory.IsNull)
                PInvoke.GlobalFree(memory);
        }
    }
}
