namespace Winshell.Commands;

internal static class DevCommand
{
    internal static readonly CommandEntry Entry = new()
    {
        Name = "dev",
        Overview = """
                          winshell dev [-c <name> | -p <path>]
                                                              run a project and apply each edit to it
                   """,
        Usage = """
                winshell dev [-c <name> | -p <path>]
                  Runs a configuration project and applies each edit to the running shell, until Ctrl+C.
                  Reactor's hot reload applies a saved C# file in place, and the other windows are left alone.
                  What does not compile shows on screen, and the last version that did keeps running.
                  Needs the .NET SDK. See the guide for what an edit can change.
                """,
        Options = [CliFlags.Path, CliFlags.Config],
        Run = Execute
    };

    internal static async Task<int> Execute(Invocation parsed)
    {
        var project = ConfigurationProject.Find(parsed.Text(CliFlags.Path), parsed.Text(CliFlags.Config),
            ShellLocations.Current.ConfigRoot, out var error);
        if (error is not null)
        {
            await Console.Error.WriteLineAsync(error);
            return 1;
        }

        if (project is null)
        {
            await Console.Error.WriteLineAsync(
                "winshell dev needs a configuration project. Run winshell new to make one.");
            return 1;
        }

        if (project.Blocked() is { } blocked)
        {
            await Console.Error.WriteLineAsync(blocked);
            return 1;
        }

        return await DevSession.Run(project);
    }
}
