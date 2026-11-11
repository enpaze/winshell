using System.Runtime.InteropServices;

namespace Winshell.Native;

// CsWin32 will not generate POINT: it tells callers to use System.Drawing.Point. Pointer helpers keep this
// one and convert where a generated call takes System.Drawing.Point. The generated RECT is the window rectangle.
[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

/// <summary>The words of a window message. CsWin32 has no declaration for them.</summary>
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
