using Winshell.Diagnostics;
using Winshell.Session;
using StartupTaskState = Windows.ApplicationModel.StartupTaskState;

namespace Winshell.Services.Tests;

internal static class SignInSentences
{
    public static DoctorCheck SignIn(string? command, bool turnedOff, string thisExe, string shellsRoot,
        Func<string, bool> exists, Func<string, string>? resolve = null, string? projectFile = null)
    {
        resolve ??= path => path;
        return DoctorSignIn.Check(SignInVerdict.FromRun(command, turnedOff, thisExe, shellsRoot, exists, resolve,
            projectFile), shellsRoot, projectFile);
    }

    public static DoctorCheck SignIn(StartupTaskState? state)
    {
        return DoctorSignIn.Check(SignInVerdict.FromTask(state));
    }

    public static DoctorCheck SignInForProcess(string processPath, string? supervisor, string? command,
        bool turnedOff, string shellsRoot, Func<string, bool> exists, Func<string, string>? resolve = null,
        string? projectFile = null)
    {
        return SignIn(command, turnedOff, SignInEntry.InstallExe(processPath, supervisor), shellsRoot, exists,
            resolve, projectFile);
    }
}
