namespace Winshell;

internal static class CommandWords
{
    public const string Run = "run";

    public const string End = "--";

    public static readonly CommandWord Path = new("--path", "-p");

    public static readonly CommandWord Shell = new("--shell", "-s");

    public static readonly CommandWord Daemonize = new("--daemonize", "-d");

    public static readonly CommandWord NoDuplicate = new("--no-duplicate", "-n");

    public static readonly CommandWord NoBuild = new("--no-build");

    public static readonly CommandWord Verbose = new("--verbose", "-v");

    public static readonly CommandWord After = new("--after");

    public static readonly CommandWord Channel = new("--channel");

    public static bool IsShellFolder(string name)
    {
        return name.Length > 0 && name is not ("." or "..") && name.IndexOfAny(['/', '\\', ':']) < 0;
    }
}

internal sealed record CommandWord(string Long, params string[] Shorts)
{
    public string? Short => Shorts.Length == 0 ? null : Shorts[0];

    public bool Is(string token)
    {
        return token == Long || Array.IndexOf(Shorts, token) >= 0;
    }
}
