using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

internal static unsafe class LowLevelMouse
{
    private static readonly Lock Gate = new();
    private static Watch[] _watches = [];
    private static HookThread? _thread;

    internal static IDisposable? Start(Action<POINT> onPress, Action<Exception> failed)
    {
        lock (Gate)
        {
            _thread ??= HookThread.Start(WINDOWS_HOOK_ID.WH_MOUSE_LL, &Proc, "Winshell mouse hook");
            if (_thread is null)
                return null;
            var watch = new Watch(onPress, failed);
            Volatile.Write(ref _watches, [watch, .. _watches]);
            return watch;
        }
    }

    private static void Stop(Watch watch)
    {
        lock (Gate)
        {
            var rest = Array.FindAll(_watches, w => w != watch);
            Volatile.Write(ref _watches, rest);
            if (rest.Length > 0 || _thread is null)
                return;
            _thread.Stop();
            _thread = null;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT Proc(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0 && lParam.Value != 0
                      && (uint)wParam.Value is PInvoke.WM_LBUTTONDOWN or PInvoke.WM_RBUTTONDOWN
                          or PInvoke.WM_MBUTTONDOWN or PInvoke.WM_XBUTTONDOWN)
        {
            var at = ((MSLLHOOKSTRUCT*)lParam.Value)->pt;
            var pt = new POINT { X = at.X, Y = at.Y };
            foreach (var watch in Volatile.Read(ref _watches))
                Callback.Run(watch.Failed, () => watch.OnPress(pt));
        }

        return PInvoke.CallNextHookEx(default, code, wParam, lParam);
    }

    private sealed class Watch(Action<POINT> onPress, Action<Exception> failed) : IDisposable
    {
        private int _stopped;

        public Action<POINT> OnPress { get; } = onPress;

        public Action<Exception> Failed { get; } = failed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _stopped, 1) == 0)
                Stop(this);
        }
    }
}
