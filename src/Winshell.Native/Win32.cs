using System.Runtime.InteropServices;

namespace Winshell.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

internal static class Win32
{
    public static int LowWord(nint value)
    {
        return (short)((long)value & 0xFFFF);
    }

    public static int HighWord(nint value)
    {
        return (short)(((long)value >> 16) & 0xFFFF);
    }
}
