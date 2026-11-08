namespace Winshell.Commands;

internal static class DevCommand
{
    internal static async Task<int> RunAsync(CliOptions options)
    {
        var project = ConfigurationProject.Find(options.Path, options.Config, ShellLocations.Current.ConfigRoot,
            out var error);
        if (error is not null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        if (project is null)
        {
            Console.Error.WriteLine("winshell dev needs a configuration project. Run winshell new to make one.");
            return 1;
        }

        if (project.Blocked() is { } blocked)
        {
            Console.Error.WriteLine(blocked);
            return 1;
        }

        return await DevSession.Run(project);
    }
}
