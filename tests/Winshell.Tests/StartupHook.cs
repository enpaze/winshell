using Winshell;

internal static class StartupHook
{
    public static void Initialize()
    {
        try
        {
            foreach (var name in WidgetRegistry.Discover(new Log()).Names)
                Console.WriteLine(name);
        }
        catch (InvalidOperationException e)
        {
            Console.Error.WriteLine(e.Message);
            Environment.Exit(1);
        }

        Environment.Exit(0);
    }
}
