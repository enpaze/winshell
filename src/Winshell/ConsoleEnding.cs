using System.Runtime.InteropServices;

namespace Winshell;

internal static class ConsoleEnding
{
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
