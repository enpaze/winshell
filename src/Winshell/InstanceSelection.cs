namespace Winshell;

/// <summary>The instance a command asked for: a process id, an id, a project path, or a config name.</summary>
internal readonly record struct InstanceChoice(int? Pid, string? Id, string? Path, string? Config)
{
    /// <summary>The line named an instance. A log with none of these may show the newest stopped instance.</summary>
    public bool Asked => Pid is not null || Id is not null || Path is not null || Config is not null;
}

/// <summary>
/// Which instance a command means. The command says whether a stopped instance can answer, and how "none" is
/// phrased. The result is that instance, or a finished failure with its sentence and exit code.
/// </summary>
internal static class InstanceSelection
{
    /// <summary>What <c>list</c> and a command that needs an instance say when nothing runs.</summary>
    internal const string NoneRunning = "No running instances. Start one with winshell run.";

    /// <summary>What <c>kill</c> says when nothing runs. It wanted nothing running, so it gets no hint to start one.</summary>
    private const string NoneToKill = "No running instances.";

    /// <summary>What the command accepts: a stopped instance as a fallback, and the sentence when nothing qualifies.</summary>
    internal readonly record struct Question(string None, bool StoppedFallback)
    {
        /// <summary>A running instance. Nothing running names the start command.</summary>
        internal static Question Running => new(NoneRunning, false);

        /// <summary>A running instance. Nothing running is a finished kill.</summary>
        internal static Question Kill => new(NoneToKill, false);

        /// <summary>A running instance, or else the stopped one a log may show. Several running instances stay an error.</summary>
        internal static Question Log => new(NoneRunning, true);
    }

    /// <summary>
    /// An instance, or a finished failure. <see cref="Notice"/> is set when the instance is the stopped one a log
    /// fell back to.
    /// </summary>
    internal readonly record struct Result(InstanceInfo? Info, string? Notice, string? Error, int Code)
    {
        public static Result Found(InstanceInfo info, string? notice = null)
        {
            return new Result(info, notice, null, 0);
        }

        public static Result Miss(string? error)
        {
            return new Result(null, null, error, 1);
        }

        public static Result Wrong(string error)
        {
            return new Result(null, null, error, 2);
        }
    }

    /// <summary>The instance <paramref name="choice"/> selects from <paramref name="listed"/>, every instance on disk, newest first.</summary>
    internal static Result Select(InstanceChoice choice, Question question,
        IReadOnlyList<(InstanceInfo Info, bool Alive)> listed)
    {
        var running = listed.Where(row => row.Alive).Select(row => row.Info).ToList();
        var selected = From(choice, running, question.None);
        if (!question.StoppedFallback || selected.Info is not null || selected.Code == 2)
            return selected;

        var stopped = choice.Asked
            ? From(choice, listed.Select(row => row.Info).ToList(), question.None).Info
            : listed.Count > 0
                ? listed[0].Info
                : null;
        return stopped is null
            ? selected
            : Result.Found(stopped, $"Showing the log of stopped instance {stopped.Id}.");
    }

    private static Result From(InstanceChoice choice, List<InstanceInfo> instances, string none)
    {
        if (choice.Pid is { } pid)
        {
            var match = instances.FirstOrDefault(i => i.Pid == pid);
            return match is null
                ? Result.Miss($"No instance with process id {pid}.")
                : Result.Found(match);
        }

        if (choice.Id is { } id)
        {
            var matches = instances.Where(i => i.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase)).ToList();
            return matches.Count switch
            {
                1 => Result.Found(matches[0]),
                0 => Result.Miss($"No instance with id {id}."),
                _ => Result.Miss($"Several instances start with {id}.")
            };
        }

        if (choice.Path is not null || choice.Config is not null)
        {
            var project = ConfigurationProject.Find(choice.Path, choice.Config, ShellLocations.Current.ConfigRoot,
                out var error);
            if (error is not null)
                return Result.Miss(error);
            var match = project!.InstancesOf(ConfigurationBuild.Run, instances).FirstOrDefault();
            return match is null
                ? Result.Miss($"No running instance of {project.Program}.")
                : Result.Found(match);
        }

        return ChooseUnselected(instances, none);
    }

    /// <summary>
    /// The instance a command with no selector acts on. One instance is that one. Several is a command-line
    /// error: kill, log and ipc must not guess.
    /// </summary>
    internal static Result ChooseUnselected(IReadOnlyList<InstanceInfo> instances, string none = NoneRunning)
    {
        if (instances.Count > 1)
        {
            var listed = string.Join(", ", instances.Select(i => $"{i.Id} ({i.ConfigPath})"));
            return Result.Wrong($"Several instances are running; pick one with -i: {listed}.");
        }

        return instances.Count == 1 ? Result.Found(instances[0]) : Result.Miss(none);
    }
}
