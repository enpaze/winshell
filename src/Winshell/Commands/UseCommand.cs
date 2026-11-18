namespace Winshell.Commands;

internal static class UseCommand
{
    internal static readonly CommandEntry Entry = new()
    {
        Name = "use",
        Overview = "       winshell use [<name>]               choose the config sign-in starts, and switch to it",
        Usage = """
                winshell use [<name>]
                  With a name, builds that config in {config-root} when it needs a build, and
                  chooses it: sign-in, the Start tile and winshell run with no -c or -p start it.
                  A shell that is running stops, and the chosen one starts in its place.
                  A failed build changes nothing.
                  With no name, lists the configs, with * before the one winshell run starts.
                """,
        Words = WordRule.One,
        Check = static parsed =>
        {
            if (parsed.Positional.Length == 1 && !CommandWords.IsConfigName(parsed.Positional[0]))
                throw new ArgumentException(
                    $"winshell use needs the name of a config folder, not \"{parsed.Positional[0]}\".");
        },
        Run = Execute
    };

    internal static async Task<int> Execute(Invocation parsed)
    {
        var locations = ShellLocations.Current;
        if (parsed.Positional.IsEmpty)
            return List(locations.ConfigRoot);
        var name = parsed.Positional[0];
        if (Choose(name, locations.ConfigRoot, ShellProject.Run, out var error) is not { } project)
        {
            await Console.Error.WriteLineAsync(error);
            return 1;
        }

        Console.WriteLine($"Chose {name}.");
        return await SwitchAsync(project, name, locations.Instances);
    }

    internal static ShellProject? Choose(string name, string root, RunProcess run, out string? error)
    {
        var project = ShellProject.Find(null, name, root, out error);
        if (project is null)
            return null;
        if (project.Prepare(false, run) is { } reason)
        {
            error = $"{reason}\nNothing changed.";
            return null;
        }

        ShellProject.Record(root, name);
        return project;
    }

    private static int List(string root)
    {
        var names = ShellProject.Names(root);
        if (names.Count == 0)
        {
            Console.WriteLine($"No configs in {root}. Run winshell new to make one.");
            return 0;
        }

        var started = ShellProject.Find(null, null, root, out _)?.Folder;
        foreach (var name in names)
        {
            var chosen = string.Equals(started, Path.Combine(root, name), StringComparison.OrdinalIgnoreCase);
            Console.WriteLine($"{(chosen ? "*" : " ")} {name}");
        }

        return 0;
    }

    private static async Task<int> SwitchAsync(ShellProject project, string name, Instances instances)
    {
        var running = instances.List(false).Select(row => row.Info).ToList();
        var own = project.InstancesOf(ShellProjectBuild.Run, running)
            .Concat(project.InstancesOf(ShellProjectBuild.Dev, running)).ToList();
        var others = running.Except(own).ToList();
        foreach (var info in others)
        {
            var quit = await ShellExit.QuitAsync(info, ExitIntent.Stop, ShellExit.Patience.Interactive,
                ShellExit.Ending.Never);
            if (!quit.Exited)
            {
                if (quit.Answer.Problem($"Instance {info.Id} refused") is { } problem)
                    await Console.Error.WriteLineAsync(problem);
                await Console.Error.WriteLineAsync(
                    $"Instance {info.Id} is still running, so {name} was not started. winshell kill --force -i {info.Id} ends it.");
                return 1;
            }

            Console.WriteLine($"Stopped {info.Id}");
        }

        if (others.Count == 0 || own.Count > 0)
            return 0;
        string[] start =
        [
            CommandWords.Run, CommandWords.NoBuild.Long, CommandWords.NoDuplicate.Long, CommandWords.Config.Long, name
        ];
        if (InstanceStart.Detach(Environment.ProcessPath!, start) is { } error)
        {
            await Console.Error.WriteLineAsync(error);
            return 1;
        }

        Console.WriteLine($"Started {name}");
        return 0;
    }
}
