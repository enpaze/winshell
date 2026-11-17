using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;

namespace Winshell.Native;

internal enum TokenRead
{
    Read,

    Denied,
    Failed
}

internal readonly unsafe ref struct ProcessHandle
{
    private ProcessHandle(HANDLE handle)
    {
        Handle = handle;
    }

    internal HANDLE Handle { get; }

    internal bool IsOpen => !Handle.IsNull;

    internal static ProcessHandle Open(uint processId)
    {
        return new ProcessHandle(
            PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, processId));
    }

    internal TokenRead ReadToken(TOKEN_INFORMATION_CLASS info, Span<byte> buffer)
    {
        if (!IsOpen)
            return TokenRead.Failed;
        HANDLE token;
        if (!PInvoke.OpenProcessToken(Handle, TOKEN_ACCESS_MASK.TOKEN_QUERY, &token))
            return (WIN32_ERROR)Marshal.GetLastPInvokeError() == WIN32_ERROR.ERROR_ACCESS_DENIED
                ? TokenRead.Denied
                : TokenRead.Failed;
        try
        {
            fixed (byte* into = buffer)
            {
                uint returned;
                return PInvoke.GetTokenInformation(token, info, into, (uint)buffer.Length, &returned)
                    ? TokenRead.Read
                    : TokenRead.Failed;
            }
        }
        finally
        {
            PInvoke.CloseHandle(token);
        }
    }

    public void Dispose()
    {
        if (IsOpen)
            PInvoke.CloseHandle(Handle);
    }
}
