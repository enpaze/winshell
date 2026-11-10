// Loaded by Tests.ps1. Names each visible top-level window that a process descended from rootProcessId shows, or
// uncloaks, while it watches. A window that closes before its event is handled, within milliseconds, escapes it.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public sealed class WinshellWindowWatch
{
    const uint EventObjectShow = 0x8002;
    const uint EventObjectUncloaked = 0x8018;
    const uint OutOfContextSkipOwnProcess = 0x0002;
    const uint WmQuit = 0x0012;
    const uint GaParent = 1;
    const int DwmwaCloaked = 14;
    const uint Th32csSnapProcess = 2;
    // Far deeper than dotnet test, its test host and a process a test starts; stops a loop of reused process ids.
    const int MaxDepth = 64;

    delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

    readonly int rootProcessId;
    readonly List<string> windows = new List<string>();
    readonly HashSet<IntPtr> seen = new HashSet<IntPtr>();
    readonly WinEventProc proc;
    readonly Thread thread;
    readonly ManualResetEventSlim hooked = new ManualResetEventSlim();
    uint threadId;
    bool failed;

    public WinshellWindowWatch(int rootProcessId)
    {
        this.rootProcessId = rootProcessId;
        proc = OnShow;
        // An out-of-context hook calls back on the thread that set it, from that thread's message loop.
        thread = new Thread(() =>
        {
            threadId = GetCurrentThreadId();
            IntPtr show = SetWinEventHook(EventObjectShow, EventObjectShow, IntPtr.Zero, proc, 0, 0, OutOfContextSkipOwnProcess);
            IntPtr uncloak = SetWinEventHook(EventObjectUncloaked, EventObjectUncloaked, IntPtr.Zero, proc, 0, 0, OutOfContextSkipOwnProcess);
            failed = show == IntPtr.Zero || uncloak == IntPtr.Zero;
            hooked.Set();
            MSG msg;
            while (!failed && GetMessage(out msg, IntPtr.Zero, 0, 0) > 0) { }
            UnhookWinEvent(show);
            UnhookWinEvent(uncloak);
        });
        thread.IsBackground = true;
        thread.Start();
        hooked.Wait();
        if (failed) { thread.Join(); throw new InvalidOperationException("Could not watch for windows: SetWinEventHook failed."); }
    }

    public string[] Stop()
    {
        PostThreadMessage(threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
        thread.Join();
        return windows.ToArray();
    }

    void OnShow(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        // A message-only window's parent is not the desktop.
        if (idObject != 0 || idChild != 0 || GetAncestor(hwnd, GaParent) != GetDesktopWindow()) return;
        int cloaked;
        if (!IsWindowVisible(hwnd) || (DwmGetWindowAttribute(hwnd, DwmwaCloaked, out cloaked, 4) == 0 && cloaked != 0)) return;
        uint pid;
        GetWindowThreadProcessId(hwnd, out pid);
        if (!seen.Add(hwnd) || !Descends((int)pid)) return;
        var title = new StringBuilder(256);
        GetWindowText(hwnd, title, title.Capacity);
        var className = new StringBuilder(256);
        GetClassName(hwnd, className, className.Capacity);
        string name;
        try { name = Process.GetProcessById((int)pid).ProcessName; } catch (Exception) { name = "exited"; }
        windows.Add(string.Format("\"{0}\" (class {1}, process {2} {3})", title, className, name, pid));
    }

    // Walks the window's process up through its parents to rootProcessId. A process whose parent has exited ends the walk.
    bool Descends(int pid)
    {
        var parents = new Dictionary<int, int>();
        IntPtr snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
        try
        {
            var entry = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32W)) };
            for (bool more = Process32FirstW(snapshot, ref entry); more; more = Process32NextW(snapshot, ref entry))
            {
                parents[(int)entry.th32ProcessID] = (int)entry.th32ParentProcessID;
            }
        }
        finally { CloseHandle(snapshot); }
        for (int depth = 0; depth < MaxDepth && pid != 0; depth++)
        {
            if (pid == rootProcessId) return true;
            if (!parents.TryGetValue(pid, out pid)) return false;
        }
        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int x; public int y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PROCESSENTRY32W
    {
        public uint dwSize; public uint cntUsage; public uint th32ProcessID; public IntPtr th32DefaultHeapID; public uint th32ModuleID;
        public uint cntThreads; public uint th32ParentProcessID; public int pcPriClassBase; public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
    }

    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint thread, uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int length);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int length);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll")] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32FirstW(IntPtr snapshot, ref PROCESSENTRY32W entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32NextW(IntPtr snapshot, ref PROCESSENTRY32W entry);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
}
