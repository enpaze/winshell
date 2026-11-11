using Winshell.Native;

namespace Winshell;

/// <summary>Ends the Win32 menus other apps have open, as a click outside a menu does.</summary>
internal static class NativeMenu
{
    private const uint CancelMode = 0x001F;

    /// <summary>
    /// Cancels every open popup menu. An app that opened its menu without the foreground, which a tray menu often is,
    /// has a menu a click elsewhere does not close, so each menu's own thread is found, not only the foreground's.
    /// </summary>
    internal static void Dismiss()
    {
        foreach (var thread in MenuThreads())
        {
            var owner = GuiThread.MenuOwner(thread);
            if (owner == 0)
                continue;
            // Finish cancellation before a button forwards another tray callback, but never let a hung app hold up
            // the shell.
            PInvoke.SendMessageTimeout((HWND)owner, CancelMode, 0, 0, SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG | SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_BLOCK, 200, out _);
        }
    }

    /// <summary>The threads with a popup menu showing. A thread's submenus are one entry.</summary>
    private static HashSet<uint> MenuThreads()
    {
        // Windows keeps a hidden menu window ready on every desktop, so only a visible one is open.
        var threads = new HashSet<uint>();
        nint menu = 0;
        while ((menu = PInvoke.FindWindowEx(HWND.Null, (HWND)menu, "#32768", null)) != 0)
            if (PInvoke.IsWindowVisible((HWND)menu))
                threads.Add(PInvoke.GetWindowThreadProcessId((HWND)menu, out _));
        return threads;
    }
}
