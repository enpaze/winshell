using System.Security;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace Winshell.Session;

/// <summary>
/// The sign-in entry. Tests use a fake, or a key of their own, and never touch the user's Run key. A packaged task
/// is faked the same way: tests never call <see cref="WindowsStartupTasks.Open"/>.
/// </summary>
internal interface IStartupRegistry
{
    /// <summary>
    /// The sign-in answer. <see cref="SignInVerdict.HasEntry"/> is whether an entry is there,
    /// <see cref="SignInVerdict.PointsElsewhere"/> whether it starts something other than this command, and
    /// <see cref="SignInVerdict.OffInWindows"/> the raw off bit, which stays set when the Run value is absent.
    /// </summary>
    SignInVerdict Read();

    bool Register();

    bool Unregister();
}

/// <summary>
/// What a sign-in registry treats as Windows refusing. Anything else is a bug: it is logged and left to surface,
/// not reported as Windows refusing.
/// </summary>
internal static class SignInAccess
{
    internal static bool IsExpected(Exception exception)
    {
        return exception is UnauthorizedAccessException or SecurityException or IOException
            or ArgumentException or NotSupportedException;
    }

    internal static T Call<T>(Func<T> action, T refused, Log log)
    {
        try
        {
            return action();
        }
        catch (Exception e) when (IsExpected(e))
        {
            return refused;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            log.Error("startup", e.ToString());
            throw;
        }
    }
}

/// <summary>A value under <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>, for the current user only.</summary>
/// <param name="command">The command this entry starts.</param>
/// <param name="configRoot">The config folder a sign-in command's <c>-c</c>, or its lack of <c>-p</c> and <c>-c</c>, is read against.</param>
/// <param name="runKey">The <c>Run</c> key, or a key of a test's own.</param>
/// <param name="approvedKey">Where Startup apps keeps its on and off for <paramref name="runKey"/>.</param>
/// <param name="log">Where a bug while reading or writing the entry is logged. Windows refusing is not.</param>
internal sealed class WindowsStartupRegistry(
    string command,
    string configRoot,
    string runKey = SignInEntry.RunKey,
    string approvedKey = SignInEntry.ApprovedKey,
    Log? log = null) : IStartupRegistry
{
    private readonly Log _log = log ?? Log.Default;

    public SignInVerdict Read()
    {
        return SignInAccess.Call(Classify, default, _log);
    }

    public bool Register()
    {
        return SignInAccess.Call(() =>
        {
            using var key = Registry.CurrentUser.CreateSubKey(runKey);
            key.SetValue(SignInEntry.Name, command, RegistryValueKind.String);
            return true;
        }, false, _log);
    }

    public bool Unregister()
    {
        return SignInAccess.Call(() =>
        {
            using var key = Registry.CurrentUser.OpenSubKey(runKey, true);
            key?.DeleteValue(SignInEntry.Name, false);
            return true;
        }, false, _log);
    }

    private SignInVerdict Classify()
    {
        // Each read refuses on its own. A Startup apps key Windows will not open leaves the Run value, and a Run
        // key it will not open leaves the off bit. One refusal of both would report no entry and not off.
        var registered = OrRefusal(() => SignInEntry.RegisteredCommand(runKey), null);
        var off = OrRefusal(() => SignInEntry.TurnedOffInWindows(approvedKey), false);
        if (registered is null)
            return new SignInVerdict { OffInWindows = off };
        return OrRefusal(() => Classified(registered, off), new SignInVerdict { HasEntry = true, OffInWindows = off });

        static T OrRefusal<T>(Func<T> read, T refused)
        {
            try
            {
                return read();
            }
            catch (Exception e) when (SignInAccess.IsExpected(e))
            {
                return refused;
            }
        }
    }

    private SignInVerdict Classified(string registered, bool off)
    {
        var wanted = CommandLine.Parse(command).ReadSignIn();
        var project = wanted.NamesConfiguration ? SignInEntry.ProjectOf(wanted, configRoot, _log) : null;
        var verdict = SignInVerdict.FromRun(registered, off, wanted.Program ?? "", configRoot, File.Exists,
            SignInEntry.Resolve, project, _log);
        // Kind follows the doctor's order, so a missing program stays a missing program. The service's elsewhere is
        // whether this command would start: when it names no configuration, that is its arguments, not the project
        // the doctor was asked about. Those two readings already differed, and both stay.
        return verdict with { HasEntry = true, OffInWindows = off, PointsElsewhere = !Starts(registered) };
    }

    /// <summary>
    /// Whether <paramref name="actual"/> starts what this entry's command starts. The windowed name counts as the
    /// console name. When the command names a configuration, <paramref name="actual"/> starts it when it runs the
    /// same install and <c>winshell run</c> would choose the same project for it, read now: adding a configuration
    /// while a shell is open changes which project the installer's command, with no <c>-p</c> and no <c>-c</c>, starts.
    /// </summary>
    private bool Starts(string actual)
    {
        var wantedLine = CommandLine.Parse(command);
        var wanted = wantedLine.ReadSignIn();
        if (!wanted.NamesConfiguration)
            return wantedLine.SameArguments(CommandLine.Parse(actual));
        var seen = CommandLine.ReadSignIn(actual);
        return wanted.Program is { } here && seen.Program is { } started &&
               SignInEntry.SameInstall(started, here, SignInEntry.Resolve, _log) &&
               SignInEntry.ProjectOf(wanted, configRoot, _log) is { } project &&
               SignInEntry.Starts(seen, project, configRoot, _log);
    }
}

/// <summary>
/// The package's <c>windows.startupTask</c>. Windows starts the package, so there is no path to go stale and
/// nothing points elsewhere. Tests pass a fake. <see cref="WindowsStartupTasks.Open"/> is the real task.
/// </summary>
internal sealed class StartupTaskRegistry(IStartupTask task, Log? log = null) : IStartupRegistry
{
    private readonly Log _log = log ?? Log.Default;

    public SignInVerdict Read()
    {
        return SignInAccess.Call(() => SignInVerdict.FromTask(task.State), default, _log);
    }

    public bool Register()
    {
        return SignInAccess.Call(() =>
        {
            var state = task.State;
            if (state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy)
                return true;
            // DisabledByUser and DisabledByPolicy stay as Windows left them. RequestEnable would not change that.
            if (state is not StartupTaskState.Disabled)
                return false;
            var next = task.RequestEnable();
            return next is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }, false, _log);
    }

    public bool Unregister()
    {
        return SignInAccess.Call(() =>
        {
            if (task.State is StartupTaskState.Disabled)
                return true;
            // Only a task this shell enabled can be disabled here. Policy and the Startup apps page are Windows'.
            if (task.State is not StartupTaskState.Enabled)
                return false;
            return task.Disable();
        }, false, _log);
    }
}

/// <summary>What <see cref="StartupTaskRegistry"/> asks Windows. A test fakes it. The real calls are <see cref="WindowsStartupTasks"/>.</summary>
internal interface IStartupTask
{
    StartupTaskState State { get; }

    /// <summary>Asks Windows to enable the task. Returns the state afterwards.</summary>
    StartupTaskState RequestEnable();

    /// <summary>Turns the task off. Returns whether it is now disabled.</summary>
    bool Disable();
}

/// <summary>
/// The real <c>StartupTask</c> that <see cref="SignInEntry.OpenTask"/> reads. Enabling or disabling it has no test:
/// that would change an installed package's sign-in.
/// </summary>
internal static class WindowsStartupTasks
{
    public static IStartupTask? Open()
    {
        return SignInEntry.OpenTask() is { } task ? new Real(task) : null;
    }

    private sealed class Real(StartupTask task) : IStartupTask
    {
        public StartupTaskState State => task.State;

        // Off the caller. Completing a WinRT async call on the shell's thread can deadlock it.
        public StartupTaskState RequestEnable() =>
#pragma warning disable VSTHRD002 // Task.Run leaves the caller, so its context is not what it waits for.
            Task.Run(async () => await task.RequestEnableAsync().AsTask().ConfigureAwait(false)).GetAwaiter()
                .GetResult();
#pragma warning restore VSTHRD002

        public bool Disable()
        {
            task.Disable();
            return task.State is StartupTaskState.Disabled;
        }
    }
}
