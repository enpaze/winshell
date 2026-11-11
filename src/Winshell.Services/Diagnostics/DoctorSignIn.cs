using Winshell.Session;

namespace Winshell.Diagnostics;

/// <summary>The doctor's check of the sign-in entry, or of a packaged Winshell's startup task.</summary>
internal static class DoctorSignIn
{
    /// <summary>The sentence for <paramref name="verdict"/>. The words are the ones <c>winshell doctor</c> prints.</summary>
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
            // Start launches winshellw.exe. Sign-in runs the winshell.exe beside it. They are one install.
            SignInKind.AnotherInstall => new DoctorCheck("signIn", "Sign-in", CheckStatus.Warn,
                $"The sign-in entry starts another winshell.exe: {verdict.Program}.",
                "Install Winshell again with -AutoStart, so the entry starts the one you use."),
            SignInKind.AnotherConfiguration => new DoctorCheck("signIn", "Sign-in", CheckStatus.Warn,
                "The sign-in entry does not start this configuration.",
                // An entry that names none starts the chosen one, so choosing this one is the fix.
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

    /// <summary>The <c>-c</c> name of <paramref name="projectFile"/>: its folder, when that is a folder of <paramref name="configRoot"/>.</summary>
    private static string? NameIn(string configRoot, string projectFile)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(projectFile));
        return string.Equals(Path.GetDirectoryName(folder), Path.TrimEndingDirectorySeparator(Path.GetFullPath(configRoot)),
            StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(folder)
            : null;
    }
}
