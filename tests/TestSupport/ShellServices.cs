namespace Winshell.TestSupport;

internal static class ShellServices
{
    internal static void Stop(this IShellService service)
    {
        service.Stop();
    }

    internal static IDisposable Stopping(this IShellService service)
    {
        return new Stopper(service);
    }

    private sealed partial class Stopper(IShellService service) : IDisposable
    {
        public void Dispose()
        {
            service.Stop();
        }
    }
}
