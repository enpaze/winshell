using Windows.Win32.Security;

namespace Winshell.Native;

internal static class ProcessElevation
{
    internal static bool? TryRead(uint processId, out bool tokenDenied)
    {
        using var process = ProcessHandle.Open(processId);
        Span<byte> elevated = stackalloc byte[sizeof(int)];
        var read = process.ReadToken(TOKEN_INFORMATION_CLASS.TokenElevation, elevated);
        tokenDenied = read == TokenRead.Denied;
        return read == TokenRead.Read ? BitConverter.ToInt32(elevated) != 0 : null;
    }
}
