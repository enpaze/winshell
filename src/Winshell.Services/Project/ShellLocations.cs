namespace Winshell;

internal sealed record ShellLocations(string ConfigRoot, string LocalRoot)
{
    public const string ConfigVariable = "WINSHELL_CONFIG_DIR";

    public const string LocalVariable = "WINSHELL_LOCAL_DIR";

    public static ShellLocations Current => new(
        RootFrom(Environment.GetEnvironmentVariable(ConfigVariable), Environment.SpecialFolder.ApplicationData),
        RootFrom(Environment.GetEnvironmentVariable(LocalVariable), Environment.SpecialFolder.LocalApplicationData));

    public string DataRoot => Path.Combine(LocalRoot, "data");

    public string StateRoot => Path.Combine(LocalRoot, "state");

    public string CacheRoot => Path.Combine(LocalRoot, "cache");

    public string RunRoot => Path.Combine(LocalRoot, "run");

    public string CrashRoot => Path.Combine(LocalRoot, "crashes");

    public Instances Instances => new(RunRoot);

    internal static string RootFrom(string? overrideValue, Environment.SpecialFolder profile)
    {
        if (!string.IsNullOrWhiteSpace(overrideValue))
            return Path.GetFullPath(overrideValue.Trim());
        return Path.Combine(Environment.GetFolderPath(profile), "winshell");
    }
}
