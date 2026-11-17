namespace Winshell.Tray;

internal sealed record TrayNames(string Window, string Explorer, string Owner)
{
    public static readonly TrayNames Session = new("Shell_TrayWnd", "Shell_TrayWnd",
        @"Local\winshell-notification-area");
}
