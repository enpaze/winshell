using System.ComponentModel;
using System.Diagnostics;

namespace Winshell;

internal static class OpenToRead
{
    public static void Open(string path, string? verb, Log log)
    {
        _ = Task.Run(() => OpenNow(path, verb, log));
    }

    private static void OpenNow(string path, string? verb, Log log)
    {
        try
        {
            var open = new ProcessStartInfo(path) { UseShellExecute = true };
            if (verb is not null)
                open.Verb = verb;
            Process.Start(open)?.Dispose();
        }
        catch (Win32Exception)
        {
            try
            {
                var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "explorer.exe");
                Process.Start(new ProcessStartInfo(explorer, $"/select,\"{path}\"") { UseShellExecute = false })
                    ?.Dispose();
            }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException)
            {
                log.Warn("notice", $"Could not open {path}: {e.Message}");
            }
        }
        catch (InvalidOperationException e)
        {
            log.Warn("notice", $"Could not open {path}: {e.Message}");
        }
    }
}
