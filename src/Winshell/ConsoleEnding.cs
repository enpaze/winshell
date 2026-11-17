using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Winshell;

internal static class ConsoleEnding
{
    internal static ShellExit.Patience PatienceFor(PosixSignal signal) =>
        signal == PosixSignal.SIGHUP ? ShellExit.Patience.Closing : ShellExit.Patience.Interactive;

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

    internal static void Quit(InstanceInfo info, ShellExit.Patience patience)
    {
#pragma warning disable VSTHRD002
        _ = ShellExit.QuitAsync(info, ExitIntent.None, patience, ShellExit.Ending.Always).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

    internal static void Kill(Process process)
    {
        try
        {
            process.Kill(true);
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception or AggregateException)
        {
        }
    }

    internal static void Wait(Task? task)
    {
        try
        {
#pragma warning disable VSTHRD002
            task?.Wait(TimeSpan.FromSeconds(4));
#pragma warning restore VSTHRD002
        }
        catch (AggregateException)
        {
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
