using Winshell.Session;

namespace Winshell.Diagnostics;

internal static class DoctorSignIn
{
    internal static DoctorCheck Check(SignInVerdict verdict, string? configRoot = null, string? projectFile = null)
    {
        return verdict.Kind switch
        {
            SignInKind.None => new DoctorCheck("signIn", "Sign-in", CheckStatus.Pass,
                "Winshell does not start when you sign in. install.ps1 -AutoStart turns that on."),
            SignInKind.NoStartupTask => new DoctorCheck("signIn", "Sign-in", CheckStatus.Warn,
                "This package has no startup task, so Winshell cannot run when you sign in."),
            SignInKind.NotRegistered => new DoctorCheck("signIn", "Sign-in", CheckStatus.Pass,
                "Winshell does not start when you sign in. Turn it on in Settings > Apps > Startup."),
            SignInKind.MissingProgram => new DoctorCheck("signIn", "Sign-in", CheckStatus.Fail,
                $"The sign-in entry starts {verdict.Program ?? verdict.Command}, which is not there.",
                "Install Winshell again with -AutoStart."),
            SignInKind.AnotherInstall => new DoctorCheck("signIn", "Sign-in", CheckStatus.Warn,
                $"The sign-in entry starts another winshell.exe: {verdict.Program}.",
                "Install Winshell again with -AutoStart, so the entry starts the one you use."),
            SignInKind.AnotherConfiguration => new DoctorCheck("signIn", "Sign-in", CheckStatus.Warn,
                "The sign-in entry does not start this configuration.",
                !verdict.NamesConfiguration && configRoot is not null && projectFile is not null &&
                NameIn(configRoot, projectFile) is { } name
                    ? $"Run winshell use {name}, so the entry starts it."
                    : "Turn on Run at sign-in for this configuration, so the entry starts it."),
            SignInKind.TurnedOffByPolicy => new DoctorCheck("signIn", "Sign-in", CheckStatus.Warn,
                "Winshell is set to start when you sign in, but Windows policy turned it off.",
                "A Windows policy controls this. Startup apps in Settings cannot turn it on."),
            SignInKind.TurnedOffInWindows => new DoctorCheck("signIn", "Sign-in", CheckStatus.Warn,
                "Winshell is set to start when you sign in, but Startup apps in Windows turned it off.",
                "Turn Winshell on in Settings > Apps > Startup."),
            _ => new DoctorCheck("signIn", "Sign-in", CheckStatus.Pass, "Winshell starts when you sign in")
        };
    }

    private static string? NameIn(string configRoot, string projectFile)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(projectFile));
        return string.Equals(Path.GetDirectoryName(folder), Path.TrimEndingDirectorySeparator(Path.GetFullPath(configRoot)),
            StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(folder)
            : null;
    }
}
