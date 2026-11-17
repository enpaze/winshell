using Winshell.Native;

namespace Winshell;

internal static class NativeMenu
{
    private const uint CancelMode = 0x001F;

    internal static void Dismiss()
    {
        foreach (var thread in MenuThreads())
        {
            var owner = GuiThread.MenuOwner(thread);
            if (owner == 0)
                continue;
            PInvoke.SendMessageTimeout((HWND)owner, CancelMode, 0, 0, SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG | SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_BLOCK, 200, out _);
        }
    }

    private static HashSet<uint> MenuThreads()
    {
        var threads = new HashSet<uint>();
        nint menu = 0;
        while ((menu = PInvoke.FindWindowEx(HWND.Null, (HWND)menu, "#32768", null)) != 0)
            if (PInvoke.IsWindowVisible((HWND)menu))
                threads.Add(PInvoke.GetWindowThreadProcessId((HWND)menu, out _));
        return threads;
    }
}
