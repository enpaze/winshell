using System.Security;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace Winshell.Session;

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

internal sealed class WindowsStartupRegistry(
    string command,
    string shellsRoot,
    string runKey = SignInEntry.RunKey,
    string approvedKey = SignInEntry.ApprovedKey,
    Log? log = null) : IStartupBackend
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
        var wanted = CommandLine.ReadSignIn(command);
        var project = wanted.NamesShell ? SignInEntry.ProjectOf(wanted, shellsRoot, _log) : null;
        var verdict = SignInVerdict.FromRun(registered, off, wanted.Program ?? "", shellsRoot, File.Exists,
            SignInEntry.Resolve, project, _log);
        return verdict with { HasEntry = true, OffInWindows = off, PointsElsewhere = !Starts(registered) };
    }

    private bool Starts(string actual)
    {
        var wantedLine = CommandLine.Parse(command);
        var wanted = CommandLine.ReadSignIn(command);
        if (!wanted.NamesShell)
            return wantedLine.SameArguments(CommandLine.Parse(actual));
        var seen = CommandLine.ReadSignIn(actual);
        return wanted.Program is { } here && seen.Program is { } started &&
               SignInEntry.SameInstall(started, here, SignInEntry.Resolve, _log) &&
               SignInEntry.ProjectOf(wanted, shellsRoot, _log) is { } project &&
               SignInEntry.Starts(seen, project, shellsRoot, _log);
    }
}

internal sealed class StartupTaskRegistry(IStartupTask task, Log? log = null) : IStartupBackend
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
            if (task.State is not StartupTaskState.Enabled)
                return false;
            return task.Disable();
        }, false, _log);
    }
}

internal interface IStartupTask
{
    StartupTaskState State { get; }

    StartupTaskState RequestEnable();

    bool Disable();
}

internal static class WindowsStartupTasks
{
    public static IStartupTask? Open()
    {
        return SignInEntry.OpenTask() is { } task ? new Real(task) : null;
    }

    private sealed class Real(StartupTask task) : IStartupTask
    {
        public StartupTaskState State => task.State;

        public StartupTaskState RequestEnable() =>
#pragma warning disable VSTHRD002
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
