namespace Winshell.Session;

internal static class SignInProcess
{
    internal static IStartupBackend? Registry(Log log)
    {
        return Choose<IStartupBackend?>(
            task => task is null ? null : new StartupTaskRegistry(task, log),
            Unpackaged);

        IStartupBackend? Unpackaged()
        {
            if (Environment.ProcessPath is not { } exe)
                return null;
            var command = ShellProject.FolderOf(exe) is null
                ? CommandLine.ForSignIn(exe, Environment.GetCommandLineArgs().Skip(1))
                : SignInEntry.Command(exe, WinshellHost.Supervising());
            return command is null
                ? null
                : new WindowsStartupRegistry(command, ShellLocations.Current.ShellsRoot, log: log);
        }
    }

    internal static SignInVerdict Read(string processPath, string shellsRoot, Func<string, bool> exists,
        Func<string, string> resolve, string? projectFile, Log? log = null)
    {
        return Choose(
            task => SignInVerdict.FromTask(task?.State),
            () =>
            {
                var supervisor = ShellProject.FolderOf(processPath) is null
                    ? null
                    : WinshellHost.Supervising();
                return SignInVerdict.FromRun(SignInEntry.RegisteredCommand(), SignInEntry.TurnedOffInWindows(),
                    SignInEntry.InstallExe(processPath, supervisor), shellsRoot, exists, resolve, projectFile, log);
            });
    }

    private static T Choose<T>(Func<IStartupTask?, T> packaged, Func<T> unpackaged)
    {
        if (!Packaging.IsPackaged)
            return unpackaged();
        return packaged(WindowsStartupTasks.Open());
    }
}
