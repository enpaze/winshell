using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

internal sealed unsafe class HookThread
{
    private readonly uint _threadId;
    private readonly Lifetime _life;

    private HookThread(Thread thread, uint threadId, Lifetime life)
    {
        Thread = thread;
        _threadId = threadId;
        _life = life;
    }

    private Thread Thread { get; }

    internal static HookThread? Start(WINDOWS_HOOK_ID hookId,
        delegate* unmanaged[Stdcall]<int, WPARAM, LPARAM, LRESULT> proc, string name, Action? lost = null)
    {
        var life = new Lifetime { Lost = lost };
        var started = new TaskCompletionSource<(nint Hook, uint ThreadId)>();
        var thread = new Thread(() => Pump(hookId, proc, started, life)) { IsBackground = true, Name = name };
        thread.Start();
#pragma warning disable VSTHRD002
        var (hook, threadId) = started.Task.Result;
#pragma warning restore VSTHRD002
        if (hook != 0)
            return new HookThread(thread, threadId, life);
        thread.Join();
        return null;
    }

    internal void Stop()
    {
        Interlocked.Exchange(ref _life.StopRequested, 1);
        PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, default, default);
        Thread.Join();
    }

    private static void Pump(WINDOWS_HOOK_ID hookId, delegate* unmanaged[Stdcall]<int, WPARAM, LPARAM, LRESULT> proc,
        TaskCompletionSource<(nint Hook, uint ThreadId)> started, Lifetime life)
    {
        PInvoke.PeekMessage(out _, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
        var hook = PInvoke.SetWindowsHookEx(hookId, proc, PInvoke.GetModuleHandle(null), 0);
        started.SetResult(((nint)hook, PInvoke.GetCurrentThreadId()));
        if (hook.IsNull)
            return;
        while (PInvoke.GetMessage(out _, HWND.Null, 0, 0) > 0)
        {
        }

        PInvoke.UnhookWindowsHookEx(hook);
        if (Volatile.Read(ref life.StopRequested) == 0)
            life.Lost?.Invoke();
    }

    private sealed class Lifetime
    {
        public int StopRequested;

        public Action? Lost { get; init; }
    }
}
