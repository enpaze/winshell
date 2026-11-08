using System.Runtime.InteropServices;

// CsWin32's generated interfaces bind to this namespace.
// ReSharper disable once CheckNamespace
namespace Windows.Win32.System.Com.StructuredStorage;

/// <summary>
/// A PROPVARIANT, declared in the namespace CsWin32 generates it in, so the Media Foundation interfaces CsWin32
/// generates use this one instead: CsWin32's own brings IStorage, whose overloads take the banned ComTypes.FILETIME.
/// The union after the type is read as two pointer-sized halves: a string is the first, a 32-bit number is the low
/// half of the first, and a BLOB is its size in the first and its data in the second, on 64-bit and 32-bit Windows
/// alike.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PROPVARIANT
{
    public ushort Type;
    public ushort Reserved1;
    public ushort Reserved2;
    public ushort Reserved3;
    public nint Value;
    public nint Extra;
}
