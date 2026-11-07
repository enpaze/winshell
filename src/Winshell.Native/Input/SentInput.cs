namespace Winshell.Native;

/// <summary>
/// When this process last sent input of its own, as a <c>GetTickCount</c> time, so a check for the user's input can
/// tell that apart from theirs: <c>GetLastInputInfo</c> counts both.
/// </summary>
internal static class SentInput
{
    private static long _last = -1;

    /// <summary>The time of the last input this process sent, or null before any.</summary>
    internal static uint? Last => Interlocked.Read(ref _last) is var last and >= 0 ? (uint)last : null;

    internal static void Mark()
    {
        Interlocked.Exchange(ref _last, Win32.GetTickCount());
    }
}
