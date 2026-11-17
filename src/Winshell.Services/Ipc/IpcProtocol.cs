namespace Winshell;

internal static class IpcProtocol
{
    public const int Current = 1;

    private const int Oldest = 0;

    public static string? ClientRefusal(int client)
    {
        return client is >= Oldest and <= Current ? null : Mismatch(Current, client);
    }

    public static string? ShellRefusal(int shell)
    {
        return shell >= Oldest ? null : Mismatch(shell, Current);
    }

    public static string? ReleaseRefusal(string? shell, string? client)
    {
        if (string.IsNullOrEmpty(shell) || string.IsNullOrEmpty(client))
            return null;
        var left = ProductVersion.Parse(shell);
        var right = ProductVersion.Parse(client);
        if (left.Major == right.Major && left.Minor == right.Minor && left.Patch == right.Patch)
            return null;
        return
            $"This shell is Winshell {shell} and this winshell is {client}. Use the winshell that matches, or build the shell with this one.";
    }

    private static string Mismatch(int shell, int client)
    {
        return
            $"This shell speaks control protocol {shell} and this winshell speaks {client}. Use the winshell that matches, or build the shell with this one.";
    }
}
