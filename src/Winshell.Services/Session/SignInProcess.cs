namespace Winshell.Session;

internal static class SignInProcess
{
    internal static IStartupRegistry? Registry(Log log)
    {
        return Choose<IStartupRegistry?>(
            task => task is null ? null : new StartupTaskRegistry(task, log),
            Unpackaged);

        IStartupRegistry? Unpackaged()
        {
            if (Environment.ProcessPath is not { } exe)
                return null;
            var command = ConfigurationProject.FolderOf(exe) is null
                ? CommandLine.ForSignIn(exe, Environment.GetCommandLineArgs().Skip(1))
                : StartupService.SignInCommand(exe, WinshellHost.Supervising());
            return command is null
                ? null
                : new WindowsStartupRegistry(command, ShellLocations.Current.ConfigRoot, log: log);
        }
    }

    internal static SignInVerdict Read(string processPath, string configRoot, Func<string, bool> exists,
        Func<string, string> resolve, string? projectFile, Log? log = null)
    {
        return Choose(
            task => SignInVerdict.FromTask(task?.State),
            () =>
            {
                var supervisor = ConfigurationProject.FolderOf(processPath) is null
                    ? null
                    : WinshellHost.Supervising();
                return SignInVerdict.FromRun(SignInEntry.RegisteredCommand(), SignInEntry.TurnedOffInWindows(),
                    SignInEntry.InstallExe(processPath, supervisor), configRoot, exists, resolve, projectFile, log);
            });
    }

    private static T Choose<T>(Func<IStartupTask?, T> packaged, Func<T> unpackaged)
    {
        if (!Packaging.IsPackaged)
            return unpackaged();
        return packaged(WindowsStartupTasks.Open());
    }
}
