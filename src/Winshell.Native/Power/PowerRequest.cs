using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;
using Windows.Win32.System.Threading;

namespace Winshell.Native;

/// <summary>A Windows power request that keeps the screen on and the PC awake until it is disposed.</summary>
internal sealed class PowerRequest : IDisposable
{
    private nint _handle;

    private PowerRequest(nint handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Keeps the screen on and the PC awake for <paramref name="reason"/>. Null when Windows refuses:
    /// <paramref name="failedStep"/> is <c>create</c> or <c>set</c>, and <paramref name="error"/> is the Windows error.
    /// </summary>
    internal static unsafe PowerRequest? TryCreate(string reason, out string failedStep, out int error)
    {
        nint handle;
        fixed (char* text = reason)
        {
            var context = new REASON_CONTEXT
            {
                Version = 0,
                Flags = POWER_REQUEST_CONTEXT_FLAGS.POWER_REQUEST_CONTEXT_SIMPLE_STRING,
                Reason = new REASON_CONTEXT._Reason_e__Union { SimpleReasonString = text }
            };
            handle = PInvoke.PowerCreateRequest(in context);
        }

        if (handle == -1 || handle == 0)
        {
            (failedStep, error) = ("create", Marshal.GetLastPInvokeError());
            return null;
        }

        if (!PInvoke.PowerSetRequest((HANDLE)handle, POWER_REQUEST_TYPE.PowerRequestDisplayRequired) ||
            !PInvoke.PowerSetRequest((HANDLE)handle, POWER_REQUEST_TYPE.PowerRequestSystemRequired))
        {
            (failedStep, error) = ("set", Marshal.GetLastPInvokeError());
            PInvoke.CloseHandle(new HANDLE(handle));
            return null;
        }

        (failedStep, error) = ("", 0);
        return new PowerRequest(handle);
    }

    /// <summary>Clears the request, so it no longer keeps the PC awake.</summary>
    public void Dispose()
    {
        if (_handle == 0)
            return;
        PInvoke.PowerClearRequest((HANDLE)_handle, POWER_REQUEST_TYPE.PowerRequestDisplayRequired);
        PInvoke.PowerClearRequest((HANDLE)_handle, POWER_REQUEST_TYPE.PowerRequestSystemRequired);
        PInvoke.CloseHandle(new HANDLE(_handle));
        _handle = 0;
    }
}
