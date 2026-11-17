using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;
using Winshell.Tray;

namespace Winshell.Services.Tests;

public sealed class TrayBackendTests : IDisposable
{
    private static readonly uint TaskbarCreated = PInvoke.RegisterWindowMessage("TaskbarCreated");
    private readonly string _id = Guid.NewGuid().ToString("N");
    private readonly ManualEventLoop _loop = new();
    private readonly Log _log;
    private readonly List<TrayBackend> _backends = [];
    private readonly nint _app;

    public TrayBackendTests(ITestOutputHelper output)
    {
        _log = new Log { Sink = (_, category, message) => output.WriteLine($"{category}: {message}") };
        _app = NativeWindow.OfClass("STATIC", "", 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, 0);
        Assert.NotEqual(0, _app);
    }

    private string ExplorerClass => $"WinshellTestExplorer-{_id}";

    private TrayNames HostNames => new($"WinshellTestTray-{_id}", ExplorerClass, $@"Local\winshell-test-tray-{_id}");

    [Fact]
    public void Apps_reach_the_host_and_explorer_still_gets_every_message()
    {
        var explorer = OpenExplorer();
        var host = Open(HostNames);
        Assert.NotEqual(0, host.Window);

        Send(host.Window, TrayCall.Build(TrayProtocol.Add, (int)_app, 1, TrayProtocol.NIF_TIP, tip: "Backup"));
        Assert.Equal("Backup", Assert.Single(host.Icons).Tip);
        Assert.Equal("Backup", Assert.Single(explorer.Icons).Tip);

        Send(host.Window, new byte[16], 0);

        Send(host.Window, TrayCall.Build(TrayProtocol.Delete, (int)_app, 1, 0));
        Assert.Empty(host.Icons);
        Assert.Empty(explorer.Icons);
    }

    [Fact]
    public void An_app_that_changes_an_icon_the_host_does_not_have_is_told_so_and_adds_it()
    {
        var explorer = OpenExplorer();
        var host = Open(HostNames);
        var guid = Guid.NewGuid();
        var flags = TrayProtocol.NIF_TIP | TrayProtocol.NIF_GUID;
        Assert.Equal(1,
            Send(explorer.Window, TrayCall.Build(TrayProtocol.Add, (int)_app, 1, flags, tip: "Security", guid: guid)));
        Assert.Equal(0,
            Send(host.Window, TrayCall.Build(TrayProtocol.Modify, (int)_app, 1, flags, tip: "Security", guid: guid)));
        Assert.Empty(host.Icons);

        Assert.Equal(1,
            Send(host.Window, TrayCall.Build(TrayProtocol.Add, (int)_app, 1, flags, tip: "Security", guid: guid)));
        Assert.Equal(1,
            Send(host.Window, TrayCall.Build(TrayProtocol.Modify, (int)_app, 1, flags, tip: "Secure", guid: guid)));
        Assert.Equal("Secure", Assert.Single(host.Icons).Tip);
    }

    [Fact]
    public void The_broadcast_reaches_message_only_windows_while_others_come_and_go()
    {
        var window = NativeWindow.OfClass("STATIC", "", 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, 0);
        Assert.NotEqual(0, window);
        var stop = new StrongBox<bool>(false);
        var churn = new Thread(() =>
        {
            while (!Volatile.Read(ref stop.Value))
                PInvoke.DestroyWindow((HWND)NativeWindow.OfClass("STATIC", "", 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, 0));
        });
        churn.Start();
        try
        {
            for (var i = 0; i < 200; i++)
            {
                var windows = WindowTree.MessageOnly();
                Assert.Contains(window, windows);
                Assert.Contains(_app, windows);
            }
        }
        finally
        {
            Volatile.Write(ref stop.Value, true);
            Assert.True(churn.Join(TimeSpan.FromMinutes(1)), "The thread that opens and closes windows did not stop");
            PInvoke.DestroyWindow((HWND)window);
        }

        Assert.DoesNotContain(window, WindowTree.MessageOnly());
    }

    [Fact]
    public void A_restarted_explorer_is_heard_and_gets_the_messages()
    {
        var explorer = OpenExplorer();
        var host = Open(HostNames);
        var created = 0;
        host.Backend.TaskbarCreated += () => created++;

        Close(explorer);
        explorer = OpenExplorer();
        PInvoke.SendMessage((HWND)host.Window, TaskbarCreated, 0, 0);
        Assert.Equal(1, created);

        Send(host.Window, TrayCall.Build(TrayProtocol.Add, (int)_app, 1, TrayProtocol.NIF_TIP, tip: "Again"));
        Assert.Equal("Again", Assert.Single(explorer.Icons).Tip);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_next_instance_has_the_notification_area_once_the_owner_exits_or_crashes(bool clean)
    {
        var names = HostNames;
        var owner = new OtherInstance(names, _log);
        var next = new TrayBackend(_loop, _log, names);
        _backends.Add(next);
        Assert.False(next.TryOwn());

        var window = owner.Window;
        owner.Exit(clean);
        if (clean)
            Assert.False(PInvoke.IsWindow((HWND)window));

        Wait.Until(next.TryOwn, "the next instance to own the notification area");
        Assert.True(next.Open(_ => true));
        Assert.NotEqual(0, next.Window);
    }

    [Fact]
    public void Lower_puts_the_host_behind_explorers_taskbar_and_raise_puts_it_back_in_front()
    {
        var explorer = OpenExplorer();
        var host = Open(HostNames);
        host.Backend.Raise();
        Assert.True(Above(host.Window, explorer.Window));

        Assert.True(host.Backend.Lower());
        Assert.True(Above(explorer.Window, host.Window));

        host.Backend.Raise();
        Assert.True(Above(host.Window, explorer.Window));
    }

    [Fact]
    public void Commands_posted_to_the_host_reach_explorers_taskbar()
    {
        var explorer = new ExplorerThread(ExplorerNames, _log);
        try
        {
            var host = Open(HostNames);
            PInvoke.PostMessage((HWND)explorer.Window, 0x8005, 0, 0);
            (uint, nint, nint)[] commands = [(PInvoke.WM_COMMAND, 0x1234, 0), (PInvoke.WM_SYSCOMMAND, 0xF120, 0)];
            foreach (var (message, wParam, lParam) in commands)
                PInvoke.PostMessage((HWND)host.Window, message, (WPARAM)(nuint)wParam, lParam);
            while (PInvoke.PeekMessage(out var msg, (HWND)host.Window, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
                PInvoke.DispatchMessage(msg);

            foreach (var command in commands)
            {
                Assert.True(explorer.Posted.TryTake(out var posted, TimeSpan.FromSeconds(5)));
                Assert.Equal(command, posted);
            }
        }
        finally
        {
            explorer.Exit();
        }
    }

    [Fact]
    public void A_busy_explorer_holds_up_an_icon_only_briefly()
    {
        using var busy = new ManualResetEventSlim();
        var explorer = new ExplorerThread(ExplorerNames, _log, busy);
        try
        {
            var host = Open(HostNames);
            Assert.Equal(1,
                Send(host.Window, TrayCall.Build(TrayProtocol.Add, (int)_app, 1, TrayProtocol.NIF_TIP, tip: "Backup")));
            var fastest = TimeSpan.MaxValue;
            for (var i = 0; i < 3; i++)
            {
                var started = TimeProvider.System.GetTimestamp();
                Assert.Equal(1,
                    Send(host.Window,
                        TrayCall.Build(TrayProtocol.Modify, (int)_app, 1, TrayProtocol.NIF_TIP, tip: "Backup")));
                var elapsed = TimeProvider.System.GetElapsedTime(started);
                if (elapsed < fastest)
                    fastest = elapsed;
            }

            Assert.Equal("Backup", Assert.Single(host.Icons).Tip);
            Assert.True(fastest < TimeSpan.FromSeconds(1),
                $"The icon waited {fastest.TotalMilliseconds} ms for Explorer");
        }
        finally
        {
            busy.Set();
            explorer.Exit();
        }
    }

    private TrayNames ExplorerNames => new(ExplorerClass, $"WinshellTestNone-{_id}",
        $@"Local\winshell-test-explorer-{Guid.NewGuid():N}");

    private sealed class ExplorerThread
    {
        private const uint Quit = 0x8001;
        private readonly Thread _thread;
        private readonly ManualResetEventSlim _started = new();

        public ExplorerThread(TrayNames names, Log log, ManualResetEventSlim? busy = null)
        {
            _thread = new Thread(() =>
                {
                    var backend = new TrayBackend(new ManualEventLoop(), log, names);
                    if (backend.TryOwn())
                        backend.Open(_ => true);
                    Window = backend.Window;
                    _started.Set();
                    busy?.Wait();
                    while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0 && !((nint)msg.hwnd == Window && msg.message == Quit))
                    {
                        if ((nint)msg.hwnd == Window && msg.message is PInvoke.WM_COMMAND or PInvoke.WM_SYSCOMMAND)
                            Posted.Add((msg.message, (nint)msg.wParam.Value, msg.lParam));
                        PInvoke.DispatchMessage(msg);
                    }

                    backend.Dispose();
                })
            { IsBackground = true };
            _thread.Start();
            _started.Wait(TestContext.Current.CancellationToken);
            Assert.NotEqual(0, Window);
        }

        public nint Window { get; private set; }

        public BlockingCollection<(uint, nint, nint)> Posted { get; } = new();

        public void Exit()
        {
            PInvoke.PostMessage((HWND)Window, Quit, 0, 0);
            Assert.True(_thread.Join(TimeSpan.FromMinutes(1)), "The stand-in Explorer did not exit");
        }
    }

    [Fact]
    public void Explorer_coming_to_the_front_is_signalled_without_waiting_for_a_timer()
    {
        var explorer = OpenExplorer();
        var host = Open(HostNames);
        host.Backend.Raise();
        Assert.True(Above(host.Window, explorer.Window));
        var signals = 0;
        host.Backend.ExplorerInFront += () => signals++;

        PInvoke.SetWindowPos((HWND)explorer.Window, HWND.HWND_TOPMOST, 0, 0, 0, 0,
            (SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
                   SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE));
        Wait.Until(() => signals > 0, "the host to hear that Explorer is in front", () =>
        {
            while (PInvoke.PeekMessage(out var msg, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
                PInvoke.DispatchMessage(msg);
        });
        Assert.True(Above(explorer.Window, host.Window));
    }

    private static bool Above(nint upper, nint lower)
    {
        for (nint hwnd = PInvoke.GetWindow((HWND)upper, GET_WINDOW_CMD.GW_HWNDNEXT);
             hwnd != 0;
             hwnd = PInvoke.GetWindow((HWND)hwnd, GET_WINDOW_CMD.GW_HWNDNEXT))
            if (hwnd == lower)
                return true;
        return false;
    }

    [Fact]
    public void An_icon_from_a_resource_is_drawn_again_at_the_size_asked_for()
    {
        using var loaded = OwnedIcon.Load(Path.Combine(Environment.SystemDirectory, "shell32.dll"), 4, "", 16);
        Assert.NotNull(loaded);
        var copied = PInvoke.CopyIcon((HICON)loaded.Handle);
        Assert.NotEqual(0, (nint)copied);
        try
        {
            var host = Open(HostNames);
            Send(host.Window,
                TrayCall.Build(TrayProtocol.Add, (int)_app, 1, TrayProtocol.NIF_ICON, icon: (int)loaded.Handle));
            Send(host.Window,
                TrayCall.Build(TrayProtocol.Add, (int)_app, 2, TrayProtocol.NIF_ICON, icon: (int)(nint)copied));
            var fromResource = host.Icons.Single(i => i.Id == 1);
            var drawn = host.Icons.Single(i => i.Id == 2);
            Assert.NotNull(fromResource.Resource);
            Assert.Null(drawn.Resource);
            Assert.Equal(16, fromResource.Image?.Width);
        }
        finally
        {
            PInvoke.DestroyIcon(copied);
        }
    }

    [Fact]
    public void An_apps_callback_reaches_its_window()
    {
        var seen = new List<string>();
        WindowProc record = (hwnd, msg, wParam, lParam) =>
        {
            if (msg == 0x8001)
                seen.Add($"callback {wParam} {lParam:X}");
            return PInvoke.DefWindowProc((HWND)hwnd, msg, (WPARAM)(nuint)wParam, lParam);
        };
        var previous = PInvoke.SetWindowLongPtr((HWND)_app, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(record));
        var backend = new TrayBackend(_loop, _log, HostNames);
        _backends.Add(backend);
        try
        {
            backend.Notify(_app, 0x8001, 1, 0x201);
            backend.Notify(_app, 0x8001, 1, 0x202);
        }
        finally
        {
            PInvoke.SetWindowLongPtr((HWND)_app, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, previous);
            GC.KeepAlive(record);
        }

        Assert.Equal(["callback 1 201", "callback 1 202"], seen);
    }

    private delegate nint WindowProc(nint hwnd, uint msg, nint wParam, nint lParam);

    private sealed record Host(TrayBackend Backend, TrayIcons Table)
    {
        public nint Window => Backend.Window;

        public IReadOnlyCollection<TrayIcon> Icons => Table.Icons;
    }

    private Host OpenExplorer()
    {
        return Open(new TrayNames(ExplorerClass, $"WinshellTestNone-{_id}",
            $@"Local\winshell-test-explorer-{Guid.NewGuid():N}"));
    }

    private Host Open(TrayNames names)
    {
        var backend = new TrayBackend(_loop, _log, names);
        _backends.Add(backend);
        var icons = new TrayIcons(backend.ReadIcon,
            window => ((int)backend.ProcessOf(window), backend.ProgramOf(window)));
        Assert.True(backend.TryOwn());
        Assert.True(backend.Open(icons.Apply));
        return new Host(backend, icons);
    }

    private void Close(Host host)
    {
        _backends.Remove(host.Backend);
        host.Backend.Dispose();
    }

    private static nint Send(nint window, byte[] payload, nuint data = TrayProtocol.NotifyIconData)
    {
        var bytes = Marshal.AllocHGlobal(payload.Length);
        var copy = Marshal.AllocHGlobal(IntPtr.Size * 3);
        try
        {
            Marshal.Copy(payload, 0, bytes, payload.Length);
            Marshal.WriteIntPtr(copy, 0, (nint)data);
            Marshal.WriteInt32(copy, IntPtr.Size, payload.Length);
            Marshal.WriteIntPtr(copy, IntPtr.Size * 2, bytes);
            return PInvoke.SendMessage((HWND)window, PInvoke.WM_COPYDATA, 0, copy);
        }
        finally
        {
            Marshal.FreeHGlobal(copy);
            Marshal.FreeHGlobal(bytes);
        }
    }

    public void Dispose()
    {
        foreach (var backend in _backends)
            backend.Dispose();
        PInvoke.DestroyWindow((HWND)_app);
    }

    private sealed class OtherInstance
    {
        private readonly Thread _thread;
        private readonly ManualResetEventSlim _started = new();
        private readonly ManualResetEventSlim _exit = new();
        private bool _clean;

        public OtherInstance(TrayNames names, Log log)
        {
            _thread = new Thread(() =>
            {
                var backend = new TrayBackend(new ManualEventLoop(), log, names);
                if (backend.TryOwn())
                    backend.Open(_ => true);
                Window = backend.Window;
                _started.Set();
                _exit.Wait();
                if (_clean)
                    backend.Dispose();
            });
            _thread.Start();
            _started.Wait(TestContext.Current.CancellationToken);
            Assert.NotEqual(0, Window);
        }

        public nint Window { get; private set; }

        public void Exit(bool clean)
        {
            _clean = clean;
            _exit.Set();
            Assert.True(_thread.Join(TimeSpan.FromMinutes(1)), "The other instance did not exit");
        }
    }
}
