using Windows.Win32;
using Windows.Win32.Security;

namespace Winshell.Native;

internal static unsafe class ProcessIntegrity
{
    internal const uint Medium = 0x2000;

    internal static uint? TryRead(uint processId)
    {
        using var process = ProcessHandle.Open(processId);
        Span<byte> label = stackalloc byte[64];
        if (process.ReadToken(TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, label) != TokenRead.Read)
            return null;
        fixed (byte* start = label)
        {
            var sid = new PSID(*(void**)start);
            var count = PInvoke.GetSidSubAuthorityCount(sid);
            if (count == null || *count == 0)
                return null;
            var level = PInvoke.GetSidSubAuthority(sid, (uint)(*count - 1));
            return level == null ? null : *level;
        }
    }
}
