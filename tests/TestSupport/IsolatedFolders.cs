namespace Winshell.TestSupport;

internal static class IsolatedFolders
{
    internal static (string Name, string Value)[] Of(string config, string local)
    {
        return [("WINSHELL_SHELLS_DIR", config), ("WINSHELL_LOCAL_DIR", local)];
    }

    internal static (string Name, string Value)[] Under(string dir)
    {
        return Of(Path.Combine(dir, "config"), Path.Combine(dir, "local"));
    }
}
