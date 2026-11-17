using System.Runtime.InteropServices;

namespace Winshell.Native;

internal static partial class SystemTimes
{
    internal static bool Read(out ulong idle, out ulong kernel, out ulong user)
    {
        return GetSystemTimes(out idle, out kernel, out user);
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
}
