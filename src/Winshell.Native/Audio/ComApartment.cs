using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Com;

namespace Winshell.Native;

internal static unsafe class ComApartment
{
    public static nint Capture(object comObject)
    {
        var unknown = Marshal.GetIUnknownForObject(comObject);
        try
        {
            IStream* stream;
            PInvoke.CoMarshalInterThreadInterfaceInStream(typeof(IUnknown).GUID, (IUnknown*)unknown, &stream)
                .ThrowOnFailure();
            return (nint)stream;
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    public static object Restore(nint stream)
    {
        PInvoke.CoGetInterfaceAndReleaseStream((IStream*)stream, out IUnknown* unknown).ThrowOnFailure();
        return Com.Wrap<object>((nint)unknown) ?? throw new InvalidComObjectException();
    }
}
