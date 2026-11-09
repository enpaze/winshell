namespace Winshell.Commands;

/// <summary>
/// <c>winshell use</c>: the configuration a run with no <c>-p</c> or <c>-c</c> starts, which is what sign-in and the
/// Start tile run. Choosing one also switches to it, so two shells never compete for the taskbar and the screen edges.
/// </summary>
internal static class UseCommand
{
    internal static async Task<int> RunAsync(CliOptions options)
    {
        var locations = ShellLocations.Current;
        if (options.Positional.IsEmpty)
            return List(locations.ConfigRoot);
        var name = options.Positional[0];
        if (Choose(name, locations.ConfigRoot, ConfigurationProject.Run, out var error) is not { } project)
        {
            await Console.Error.WriteLineAsync(error);
            return 1;
        }

        Console.WriteLine($"Chose {name}.");
        return await SwitchAsync(project, name, locations.Instances);
    }

    /// <summary>
    /// Builds the configuration <paramref name="name"/> when it needs a build, so the next sign-in can start it, then
    /// records it. Null, with why, when it is not there or did not build; the earlier choice stays.
    /// </summary>
    internal static ConfigurationProject? Choose(string name, string root, RunProcess run, out string? error)
    {
        var project = ConfigurationProject.Find(null, name, root, out error);
        if (project is null)
            return null;
        if (project.Prepare(false, run) is { } reason)
        {
            error = $"{reason}\nNothing changed.";
            return null;
        }

        ConfigurationProject.Choose(root, name);
        return project;
    }

    /// <summary>Lists the configurations in <paramref name="root"/>, with <c>*</c> before the one a bare run starts.</summary>
    private static int List(string root)
    {
        var names = ConfigurationProject.Names(root);
        if (names.Count == 0)
        {
            Console.WriteLine($"No configs in {root}. Run winshell new to make one.");
            return 0;
        }

        var started = ConfigurationProject.Find(null, null, root, out var error)?.Folder;
        foreach (var name in names)
        {
            var chosen = string.Equals(started, Path.Combine(root, name), StringComparison.OrdinalIgnoreCase);
            Console.WriteLine($"{(chosen ? "*" : " ")} {name}");
        }

        if (error is null)
            return 0;
        Console.Error.WriteLine(error);
        return 1;
    }

    /// <summary>
    /// Stops every running shell but <paramref name="project"/>'s, each until it has exited, then starts the chosen one
    /// in the background as <c>winshell run</c> would. With nothing running, nothing starts.
    /// </summary>
    private static async Task<int> SwitchAsync(ConfigurationProject project, string name, Instances instances)
    {
        var running = instances.List(false).Select(row => row.Info).ToList();
        var own = project.InstancesOf(ConfigurationBuild.Run, running)
            .Concat(project.InstancesOf(ConfigurationBuild.Dev, running)).ToList();
        var others = running.Except(own).ToList();
        foreach (var info in others)
        {
            // Its supervisor must not start it again.
            var quit = await ShellExit.QuitAsync(info, ExitIntent.Stop, ShellExit.Patience.Interactive,
                ShellExit.Ending.Never);
            if (!quit.Exited)
            {
                if (quit.Answer.Problem($"Instance {info.Id} refused") is { } problem)
                    await Console.Error.WriteLineAsync(problem);
                await Console.Error.WriteLineAsync(
                    $"Instance {info.Id} is still running, so {name} did not start. winshell kill --force ends it.");
                return 1;
            }

            Console.WriteLine($"Stopped {info.Id}");
        }

        if (others.Count == 0 || own.Count > 0)
            return 0;
        // The build is done, so the background run does not build again.
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
