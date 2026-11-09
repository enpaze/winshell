using System.Diagnostics;

namespace OmarchyStyle;

internal static class SettingsPage
{
    /// <summary>Opens a Windows Settings page, such as <c>ms-settings:sound</c>.</summary>
    public static void Open(string page)
    {
        Process.Start(new ProcessStartInfo(page) { UseShellExecute = true })?.Dispose();
    }
}
