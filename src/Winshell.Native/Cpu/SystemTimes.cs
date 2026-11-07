using System.Runtime.InteropServices;

namespace Winshell.Native;

/// <summary>The time the processors have spent since Windows started, summed over every core.</summary>
internal static partial class SystemTimes
{
    /// <summary>
    /// Reads the idle, kernel and user time, each in 100 ns ticks. Kernel time includes idle time. Returns false when
    /// Windows fails.
    /// </summary>
    internal static bool Read(out ulong idle, out ulong kernel, out ulong user)
    {
        return GetSystemTimes(out idle, out kernel, out user);
    }

    /// <summary>
    /// Handwritten: CsWin32's declaration takes the banned <c>System.Runtime.InteropServices.ComTypes.FILETIME</c>. A
    /// <c>FILETIME</c> is the low and high halves of a 64-bit tick count, as a <see cref="ulong"/> lays them out.
    /// </summary>
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
}
