namespace Winshell;

public sealed class ShellPaths
{
    private readonly ShellIdentity _identity;

    internal ShellPaths(ShellIdentity identity, string logs, string program, string shells)
    {
        _identity = identity;
        Logs = logs;
        Program = program;
        Shells = shells;
    }

    public string Data => Created(_identity.Data);

    public string State => Created(_identity.State);

    public string Cache => Created(_identity.Cache);

    public string Logs { get; }

    public string Program { get; }

    public string Shells { get; }

    private static string Created(string folder)
    {
        Directory.CreateDirectory(folder);
        return folder;
    }
}
