using Winshell.Diagnostics;
using Winshell.Session;
using StartupTaskState = Windows.ApplicationModel.StartupTaskState;

namespace Winshell.Services.Tests;

/// <summary>
/// The sentence <c>winshell doctor</c> prints for a sign-in answer. Production reads this process and calls
/// <see cref="DoctorSignIn.Check"/>; these build that answer from the facts a test supplies.
/// </summary>
internal static class SignInSentences
{
    public static DoctorCheck SignIn(string? command, bool turnedOff, string thisExe, string configRoot,
        Func<string, bool> exists, Func<string, string>? resolve = null, string? projectFile = null)
    {
        resolve ??= path => path;
        return DoctorSignIn.Check(SignInVerdict.FromRun(command, turnedOff, thisExe, configRoot, exists, resolve,
            projectFile), configRoot, projectFile);
    }

    public static DoctorCheck SignIn(StartupTaskState? state)
    {
        return DoctorSignIn.Check(SignInVerdict.FromTask(state));
    }

    public static DoctorCheck SignInForProcess(string processPath, string? supervisor, string? command,
        bool turnedOff, string configRoot, Func<string, bool> exists, Func<string, string>? resolve = null,
        string? projectFile = null)
    {
        return SignIn(command, turnedOff, SignInEntry.InstallExe(processPath, supervisor), configRoot, exists,
            resolve, projectFile);
    }
}
