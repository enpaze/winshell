using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Winshell;

/// <summary>
/// Ctrl+C, Ctrl+Break and the console closing, for <c>winshell run</c> and <c>winshell dev</c>.
/// A supervisor is told only by <c>kill</c>, <c>reload</c> and <c>use</c>, so <see cref="Quit"/> and
/// <see cref="ShellProcess.StopAsync"/> pass <see cref="ExitIntent.None"/>.
/// </summary>
internal static class ConsoleEnding
{
    /// <summary>
    /// Closing the console leaves about five seconds. Ctrl+C and Ctrl+Break wait with the user.
    /// </summary>
    internal static ShellExit.Patience PatienceFor(PosixSignal signal) =>
        signal == PosixSignal.SIGHUP ? ShellExit.Patience.Closing : ShellExit.Patience.Interactive;

    /// <summary>Registers the three signals. <paramref name="stop"/> runs on the handler thread.</summary>
    internal static IDisposable Listen(Action<PosixSignalContext> stop)
    {
        PosixSignalRegistration? interrupt = null;
        PosixSignalRegistration? breaks = null;
        PosixSignalRegistration? close = null;
        try
        {
            interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, stop);
            breaks = PosixSignalRegistration.Create(PosixSignal.SIGQUIT, stop);
            close = PosixSignalRegistration.Create(PosixSignal.SIGHUP, stop);
            IDisposable held = new Registrations(interrupt, breaks, close);
            // Registrations owns them. Clearing the locals keeps the finally from disposing the value this returns.
            interrupt = breaks = close = null;
            return held;
        }
        finally
        {
            close?.Dispose();
            breaks?.Dispose();
            interrupt?.Dispose();
        }
    }

    /// <summary>Asks the shell to quit and waits only as long as <paramref name="patience"/> allows.</summary>
    internal static void Quit(InstanceInfo info, ShellExit.Patience patience)
    {
#pragma warning disable VSTHRD002 // A signal handler must finish before Windows ends this process. No thread here runs a continuation it waits for.
        _ = ShellExit.QuitAsync(info, ExitIntent.None, patience, ShellExit.Ending.Always).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

    /// <summary>Ends <paramref name="process"/> and what it started.</summary>
    internal static void Kill(Process process)
    {
        try
        {
            process.Kill(true);
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception or AggregateException)
        {
            // The process is already gone, or Windows is tearing the handler down.
        }
    }

    /// <summary>
    /// Blocks on <paramref name="task"/> for at most four seconds. Closing the console ends this process once the
    /// handler returns, which leaves about that long.
    /// </summary>
    internal static void Wait(Task? task)
    {
        try
        {
#pragma warning disable VSTHRD002 // A handler must finish before Windows ends this process. Supervision runs on the pool.
            task?.Wait(TimeSpan.FromSeconds(4));
#pragma warning restore VSTHRD002
        }
        catch (AggregateException)
        {
            // The process is already gone, or Windows is tearing the handler down.
        }
    }

    private sealed class Registrations(
        PosixSignalRegistration interrupt,
        PosixSignalRegistration breaks,
        PosixSignalRegistration close) : IDisposable
    {
        public void Dispose()
        {
            close.Dispose();
            breaks.Dispose();
            interrupt.Dispose();
        }
    }
}
