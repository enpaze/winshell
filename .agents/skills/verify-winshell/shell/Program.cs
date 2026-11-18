using FeatureMap;
using Winshell;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Shell.Run<FeatureShell>();
    }
}
