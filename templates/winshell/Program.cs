using Winshell;

namespace MyShell;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Shell.Run<AppShell>();
    }
}
