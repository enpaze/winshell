namespace Winshell.Diagnostics;

internal static class DoctorProject
{
    internal static IReadOnlyList<DoctorCheck> Configuration(ConfigurationProject? project, string? problem)
    {
        return
        [
            Doctor.Guard("project", "Project", () => Project(project, problem)),
            Doctor.Guard("built", "Built", () => Built(project)),
            Doctor.Guard("build", "Build", () => BuildNeeded(project))
        ];
    }

    private static DoctorCheck Project(ConfigurationProject? project, string? problem)
    {
        if (problem is not null)
            return new DoctorCheck("project", "Project", CheckStatus.Fail, problem,
                "Pick a config with winshell -c <name>, or give its folder with -p, and make sure your account can read it.");
        if (project is null)
            return new DoctorCheck("project", "Project", CheckStatus.Warn,
                "No configuration project, so winshell run starts nothing.",
                "Run winshell new to make one.");
        return new DoctorCheck("project", "Project", CheckStatus.Pass, project.ProjectFile);
    }

    private static DoctorCheck Built(ConfigurationProject? project)
    {
        if (project is null)
            return new DoctorCheck("built", "Built", CheckStatus.Pass, "No configuration project to build.");
        if (!File.Exists(project.Program))
            return new DoctorCheck("built", "Built", CheckStatus.Fail, $"{project.Name} has not been built.",
                "Run winshell run once to build it.");
        return new DoctorCheck("built", "Built", CheckStatus.Pass, project.Program);
    }

    private static DoctorCheck BuildNeeded(ConfigurationProject? project)
    {
        if (project is null)
            return new DoctorCheck("build", "Build", CheckStatus.Pass, "No configuration project to build.");
        if (!File.Exists(project.Program))
            return new DoctorCheck("build", "Build", CheckStatus.Pass,
                $"Nothing to compare until {project.Name} is built.");
        if (!project.NeedsBuild())
            return new DoctorCheck("build", "Build", CheckStatus.Pass, $"{project.Name} is up to date.");
        return new DoctorCheck("build", "Build", CheckStatus.Warn,
            $"A file in {project.Name} is newer than the program.",
            "Run winshell run to build it. Signing in uses the last build.");
    }
}
