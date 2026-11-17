using System.Runtime.InteropServices;

namespace Windows.Win32.System.Com.StructuredStorage;

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
