namespace Winshell;

internal readonly record struct InstanceChoice(int? Pid, string? Id, string? Path, string? Config)
{
    public bool Asked => Pid is not null || Id is not null || Path is not null || Config is not null;
}

internal static class InstanceSelection
{
    internal const string NoneRunning = "No running instances. Start one with winshell run.";

    private const string NoneToKill = "No running instances.";

    internal readonly record struct Question(string None, bool StoppedFallback)
    {
        internal static Question Running => new(NoneRunning, false);

        internal static Question Kill => new(NoneToKill, false);

        internal static Question Log => new(NoneRunning, true);
    }

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
            var project = ShellProject.Find(choice.Path, choice.Config, ShellLocations.Current.ConfigRoot,
                out var error);
            if (error is not null)
                return Result.Miss(error);
            var match = project!.InstancesOf(ShellProjectBuild.Run, instances).FirstOrDefault();
            return match is null
                ? Result.Miss($"No running instance of {project.Program}.")
                : Result.Found(match);
        }

        return ChooseUnselected(instances, none);
    }

    internal static Result ChooseUnselected(IReadOnlyList<InstanceInfo> instances, string none = NoneRunning)
    {
        if (instances.Count > 1)
        {
            var listed = string.Join(", ", instances.Select(i => $"{i.Id} ({i.Program})"));
            return Result.Wrong($"Several instances are running; pick one with -i: {listed}.");
        }

        return instances.Count == 1 ? Result.Found(instances[0]) : Result.Miss(none);
    }
}
