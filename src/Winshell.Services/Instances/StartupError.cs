using System.Diagnostics;
using Winshell.Native;

namespace Winshell;

internal static class StartupError
{
    internal static void Report(string message, string? folder)
    {
        try
        {
            Console.Error.WriteLine(message);
        }
        catch (IOException)
        {
        }

        if (NeedsDialog(ProcessConsole.Window(), ProcessConsole.Target(StandardStream.Error)))
            Dialog(message, folder);
    }

    internal static bool NeedsDialog(nint consoleWindow, StreamTarget stderr)
    {
        return consoleWindow == 0 && stderr is not (StreamTarget.File or StreamTarget.Pipe);
    }

    internal static string? FolderFor(string? path, string configRoot)
    {
        if (path is not null)
        {
            var full = Path.GetFullPath(path);
            if (Directory.Exists(full))
                return full;
            var parent = Path.GetDirectoryName(full);
            return parent is not null && Directory.Exists(parent) ? parent : null;
        }

        return Directory.Exists(configRoot) ? configRoot : null;
    }

    private static void Dialog(string message, string? folder)
    {
        try
        {
            var pressed = TaskDialog.ShowError("Winshell", "Winshell could not start", message,
                folder is null ? null : (OpenConfigFolder, "Open config folder"));
            if (pressed == OpenConfigFolder && folder is not null)
                Open(folder);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
        }
    }

    private static void Open(string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
                return;
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
        }
    }

    private const int OpenConfigFolder = 100;
}
