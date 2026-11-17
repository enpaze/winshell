using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Com;

namespace Winshell.Native;

internal static unsafe class Com
{
    internal static T? Create<T>(Guid clsid, CLSCTX context, out int hr) where T : class
    {
        var iid = typeof(T).GUID;
        void* created = null;
        hr = PInvoke.CoCreateInstance(&clsid, null, context, &iid, &created).Value;
        return hr < 0 ? null : Wrap<T>((nint)created);
    }

    internal static T? Wrap<T>(nint pointer) where T : class
    {
        if (pointer == 0)
            return null;
        try
        {
            var wrapper = Marshal.GetObjectForIUnknown(pointer);
            if (wrapper is T wrapped)
                return wrapped;
            Release(wrapper);
            return null;
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }

    internal static void Release(object? com)
    {
        if (com is not null && Marshal.IsComObject(com))
            Marshal.ReleaseComObject(com);
    }
}
