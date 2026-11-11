using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

/// <summary>
/// Hosts one low-level hook on a thread of its own, for the hooks in this assembly. Windows removes a low-level hook that misses its timeout,
/// without telling anyone, so a busy loop thread must not be the one that serves it. The thread installs the hook,
/// waits in GetMessage, and unhooks on WM_QUIT. The procedure runs on that thread, so it must not touch state that
/// belongs to the loop thread; it posts work to the loop instead.
/// </summary>
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

    /// <summary>
    /// Starts the thread and waits for the hook to be installed. Null if Windows refused the hook.
    /// <paramref name="lost"/> runs on the hook thread if that thread ends without <see cref="Stop"/>,
    /// after the hook is removed. Windows can also remove the hook and leave the thread running, and then
    /// <paramref name="lost"/> does not run.
    /// </summary>
    internal static HookThread? Start(WINDOWS_HOOK_ID hookId,
        delegate* unmanaged[Stdcall]<int, WPARAM, LPARAM, LRESULT> proc, string name, Action? lost = null)
    {
        var life = new Lifetime { Lost = lost };
        var started = new TaskCompletionSource<(nint Hook, uint ThreadId)>();
        var thread = new Thread(() => Pump(hookId, proc, started, life)) { IsBackground = true, Name = name };
        thread.Start();
#pragma warning disable VSTHRD002 // The thread it waits for runs no continuation of this one.
        var (hook, threadId) = started.Task.Result;
#pragma warning restore VSTHRD002
        if (hook != 0)
            return new HookThread(thread, threadId, life);
        thread.Join();
        return null;
    }

    /// <summary>Removes the hook and waits for its thread to end.</summary>
    internal void Stop()
    {
        Interlocked.Exchange(ref _life.StopRequested, 1);
        PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, default, default);
        Thread.Join();
    }

    private static void Pump(WINDOWS_HOOK_ID hookId, delegate* unmanaged[Stdcall]<int, WPARAM, LPARAM, LRESULT> proc,
        TaskCompletionSource<(nint Hook, uint ThreadId)> started, Lifetime life)
    {
        // Creates the thread's message queue, so the quit message posted by Stop is not lost.
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
