using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Winshell.Native;

internal sealed unsafe class WinEventHook : IDisposable
{
    private static readonly ConcurrentDictionary<nint, WinEventHook> Hooks = new();

    private readonly Action<uint, nint, int, int> _onEvent;
    private readonly Action<Exception>? _failed;
    private nint _hook;

    private WinEventHook(nint hook, Action<uint, nint, int, int> onEvent, Action<Exception>? failed)
    {
        _hook = hook;
        _onEvent = onEvent;
        _failed = failed;
    }

    internal static WinEventHook? Start(uint eventType, Action<uint, nint, int, int> onEvent,
        Action<Exception>? failed = null)
    {
        return Start(eventType, eventType, onEvent, failed);
    }

    internal static WinEventHook? Start(uint min, uint max, Action<uint, nint, int, int> onEvent,
        Action<Exception>? failed = null)
    {
        var handle = PInvoke.SetWinEventHook(min, max, default, &OnEvent, 0, 0, PInvoke.WINEVENT_OUTOFCONTEXT);
        if (handle.IsNull)
            return null;
        var hook = new WinEventHook((nint)handle, onEvent, failed);
        Hooks[handle] = hook;
        return hook;
    }

    public void Dispose()
    {
        if (_hook == 0)
            return;
        Hooks.TryRemove(_hook, out _);
        PInvoke.UnhookWinEvent((HWINEVENTHOOK)_hook);
        _hook = 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnEvent(HWINEVENTHOOK hook, uint eventType, HWND hwnd, int idObject, int idChild, uint thread,
        uint time)
    {
        if (Hooks.TryGetValue((nint)hook, out var owner))
            Callback.Run(owner._failed, () => owner._onEvent(eventType, (nint)hwnd, idObject, idChild));
    }
}
