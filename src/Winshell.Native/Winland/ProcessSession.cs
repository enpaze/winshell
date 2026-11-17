using Windows.Win32;

namespace Winshell.Native;

internal static class ProcessSession
{
    internal static uint? Of(uint processId)
    {
        return PInvoke.ProcessIdToSessionId(processId, out var session) ? session : null;
    }
}
