using System.Runtime.InteropServices;
using Windows.Foundation;
using Winshell.Capture;
using Winshell.Native;
using Winshell.Windows;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class CaptureSessionTests
{
    private static readonly CaptureTarget Screen = new(0, "display");

    private static PixelImage Red(int width = 4, int height = 2)
    {
        var bgra = new byte[width * height * 4];
        for (var i = 0; i < bgra.Length; i += 4)
            (bgra[i + 2], bgra[i + 3]) = (255, 255);
        return new PixelImage(width, height, bgra);
    }

    [TestMethod]
    public void Start_is_explicit_and_stop_clears_pixels_and_cancels_reads()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeCapture();
        using var session = new CaptureSession(loop, () => backend);
        session.Target = Screen;
        session.IncludeCursor = true;
        Assert.IsFalse(session.Running);
        Assert.AreEqual(0, backend.Starts);

        Assert.IsTrue(session.Start());
        Assert.IsTrue(session.Running);
        Assert.IsTrue(backend.Cursor);
        Assert.AreEqual(Screen, backend.Target);
        var ready = 0;
        session.FrameReady += () => ready++;
        var frame = Red();
        backend.Next = frame;
        loop.Advance(TimeSpan.FromMilliseconds(33));

        Assert.AreSame(frame, session.Frame);
        Assert.IsTrue(session.HasFrame);
        Assert.AreEqual(new Size(4, 2), session.PixelSize);
        Assert.AreEqual(1, ready);

        session.Stop();

        Assert.IsFalse(session.Running);
        Assert.IsFalse(session.HasFrame);
        Assert.IsNull(session.Frame);
        Assert.AreEqual(new Size(0, 0), session.PixelSize);
        Assert.AreEqual(1, backend.Disposals);
        loop.Advance(TimeSpan.FromSeconds(10));
        Assert.AreEqual(1, backend.Reads);
    }

    [TestMethod]
    public void No_new_frame_keeps_the_last_one_and_raises_nothing()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeCapture { Next = Red() };
        using var session = new CaptureSession(loop, () => backend);
        session.Target = Screen;
        var ready = 0;
        session.FrameReady += () => ready++;
        Assert.IsTrue(session.Start());

        loop.Advance(TimeSpan.FromMilliseconds(66));

        Assert.IsTrue(session.HasFrame);
        Assert.AreEqual(1, ready);
        Assert.AreEqual(2, backend.Reads);
    }

    [TestMethod]
    public void Changing_the_target_stops_and_a_stale_close_cannot_stop_the_next_capture()
    {
        var loop = new ManualEventLoop();
        var backends = new List<FakeCapture>();
        using var session = new CaptureSession(loop, () =>
        {
            var backend = new FakeCapture();
            backends.Add(backend);
            return backend;
        });
        session.Target = Screen;
        Assert.IsTrue(session.Start());
        var stale = backends[0].Closed!;

        session.Target = CaptureTarget.ForWindow(123);
        Assert.IsFalse(session.Running);
        Assert.AreEqual(1, backends[0].Disposals);

        Assert.IsTrue(session.Start());
        Assert.AreEqual(new CaptureTarget(123, ""), backends[1].Target);
        stale();
        loop.Process();
        Assert.IsTrue(session.Running);
    }

    [TestMethod]
    public void An_app_window_is_captured_without_its_handle_and_closing_it_stops()
    {
        var loop = new ManualEventLoop();
        var windows = new FakeWindowBackend();
        windows.Show(10);
        windows.Show(20);
        using var list = new WindowListService(loop, Log.Default, backend: windows);
        var first = list.Windows.Single(window => window.Hwnd == 10);
        var second = list.Windows.Single(window => window.Hwnd == 20);
        var backends = new List<FakeCapture>();
        using var session = new CaptureSession(loop, () =>
        {
            var backend = new FakeCapture();
            backends.Add(backend);
            return backend;
        });
        session.Target = first;

        Assert.AreSame(first, session.Target!.Source);
        Assert.AreEqual(new CaptureTarget(10, ""), new CaptureTarget(session.Target.Window, session.Target.MonitorId));
        Assert.IsTrue(session.Start());
        Assert.AreEqual(new CaptureTarget(10, ""), backends[0].Target);
        Assert.IsTrue(session.Running);

        session.Target = first;
        Assert.IsTrue(session.Running);

        session.Target = second;
        Assert.IsFalse(session.Running);
        Assert.IsTrue(session.Start());
        Assert.AreEqual(new CaptureTarget(20, ""), backends[1].Target);

        windows.Destroy(10);
        loop.Process();
        Assert.IsTrue(session.Running);
        Assert.AreSame(second, session.Target!.Source);

        windows.Destroy(20);
        loop.Process();
        Assert.IsFalse(session.Running);
        Assert.IsNull(session.Target);
        Assert.AreEqual(1, backends[1].Disposals);
    }

    [TestMethod]
    public void A_source_that_closes_stops_on_the_loop()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeCapture();
        using var session = new CaptureSession(loop, () => backend);
        session.Target = Screen;
        Assert.IsTrue(session.Start());

        backend.Closed!();
        Assert.IsTrue(session.Running);
        loop.Process();

        Assert.IsFalse(session.Running);
        Assert.AreEqual(1, backend.Disposals);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Denial_and_device_errors_release_resources_and_report_why(bool duringRead)
    {
        var loop = new ManualEventLoop();
        var backend = new FakeCapture { FailStart = !duringRead, FailRead = duringRead };
        using var session = new CaptureSession(loop, () => backend);
        session.Target = Screen;

        Assert.AreEqual(duringRead, session.Start());
        loop.Advance(TimeSpan.FromMilliseconds(33));

        Assert.IsFalse(session.Running);
        Assert.IsNotEmpty(session.Error);
        Assert.AreEqual(1, backend.Disposals);
    }

    [TestMethod]
    public void A_session_with_no_target_does_not_make_a_backend()
    {
        using var session = new CaptureSession(new ManualEventLoop(),
            () => throw new InvalidOperationException("Must not create"));

        Assert.IsFalse(session.Start());

        Assert.Contains("Choose", session.Error, StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_teardown_error_still_clears_pixels()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeCapture { Next = Red(), FailDispose = true };
        var session = new CaptureSession(loop, () => backend) { Target = Screen };
        Assert.IsTrue(session.Start());
        loop.Advance(TimeSpan.FromMilliseconds(33));

        session.Dispose();

        Assert.IsFalse(session.Running);
        Assert.IsFalse(session.HasFrame);
        Assert.IsNotEmpty(session.Error);
        loop.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(1, backend.Reads);
        Assert.IsFalse(session.Start());
    }

    [TestMethod]
    public void The_interval_is_bounded_and_a_frame_handler_can_stop_capture()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeCapture { Next = Red() };
        using var session = new CaptureSession(loop, () => backend);
        session.Target = Screen;
        session.FrameInterval = 0;
        Assert.AreEqual(16, session.FrameInterval);
        session.FrameInterval = int.MaxValue;
        Assert.AreEqual(1000, session.FrameInterval);
        session.FrameInterval = 16;
        session.FrameReady += session.Stop;

        Assert.IsTrue(session.Start());
        loop.Advance(TimeSpan.FromSeconds(5));

        Assert.IsFalse(session.Running);
        Assert.AreEqual(1, backend.Reads);
    }

    [TestMethod]
    public void A_saved_png_never_overwrites_a_file_and_never_goes_to_a_remote_url()
    {
        var directory = TempFolder.NewPath();
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "test.png");
        var loop = new ManualEventLoop();
        var backend = new FakeCapture { Next = Red(8, 6) };
        using var session = new CaptureSession(loop, () => backend);
        session.Target = Screen;
        try
        {
            Assert.IsFalse(session.SaveToFile(path));
            Assert.IsTrue(session.Start());
            loop.Advance(TimeSpan.FromMilliseconds(33));

            Assert.IsTrue(session.SaveToFile(path));
            Assert.IsEmpty(session.Error);
            var saved = File.ReadAllBytes(path);
            Assert.AreSequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, saved[..4]);

            File.WriteAllText(path, "kept");
            Assert.IsFalse(session.SaveToFile(path));
            Assert.IsNotEmpty(session.Error);
            Assert.AreEqual("kept", File.ReadAllText(path));

            Assert.IsFalse(session.SaveToFile("https://example.com/test.png"));
            Assert.IsFalse(session.SaveToFile(""));
            Assert.IsFalse(session.SaveToFile(Path.Combine(directory, "missing", "test.png")));
            Assert.IsTrue(session.SaveToFile(new Uri(Path.Combine(directory, "url.png")).AbsoluteUri));
            Assert.IsTrue(File.Exists(Path.Combine(directory, "url.png")));
        }
        finally
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
        }
    }

    [TestMethod]
    public void Copying_needs_a_frame_and_a_window_and_reports_a_failure()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeCapture { Next = Red() };
        using var session = new CaptureSession(loop, () => backend);
        session.Target = Screen;
        var gone = NativeWindow.OfClass("STATIC", "", 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, 0);
        Assert.AreNotEqual(0, gone);
        Assert.IsTrue(PInvoke.DestroyWindow((HWND)gone));

        Assert.IsFalse(session.CopyToClipboard(gone));
        Assert.Contains("open window", session.Error, StringComparison.Ordinal);
        Assert.IsTrue(session.Start());
        loop.Advance(TimeSpan.FromMilliseconds(33));
        Assert.IsFalse(session.CopyToClipboard(0));
        Assert.Contains("open window", session.Error, StringComparison.Ordinal);

        Assert.IsFalse(session.CopyToClipboard(gone));
        Assert.IsNotEmpty(session.Error);
        Assert.DoesNotContain("open window", session.Error, StringComparison.Ordinal);
    }

    [TestMethod]
    public void The_clipboard_gets_a_top_down_header_and_straight_alpha()
    {
        var dib = CaptureClipboard.EncodeDib(new PixelImage(2, 1, [64, 32, 16, 128, 0, 0, 255, 255], true));

        Assert.AreEqual(132, dib.Length);
        Assert.AreEqual(124, BitConverter.ToInt32(dib, 0));
        Assert.AreEqual(2, BitConverter.ToInt32(dib, 4));
        Assert.AreEqual(-1, BitConverter.ToInt32(dib, 8));
        Assert.AreEqual(32, BitConverter.ToInt16(dib, 14));
        Assert.AreEqual(3, BitConverter.ToInt32(dib, 16));
        Assert.AreEqual(0x00ff0000u, BitConverter.ToUInt32(dib, 40));
        Assert.AreEqual(0xff000000u, BitConverter.ToUInt32(dib, 52));
        Assert.AreSequenceEqual(new byte[] { 128, 64, 32, 128, 0, 0, 255, 255 }, dib[124..]);
    }

    [TestMethod]
    [DataRow("C:\\shots\\a.png", "C:\\shots\\a.png")]
    [DataRow("a.png", "a.png")]
    [DataRow("file:///C:/shots/a.png", "C:\\shots\\a.png")]
    [DataRow("https://example.com/a.png", null)]
    [DataRow("data:image/png;base64,AAAA", null)]
    [DataRow(" ", null)]
    public void Only_a_local_path_is_a_place_to_save(string path, string? local)
    {
        Assert.AreEqual(local, CaptureSession.LocalPath(path));
    }

    [TestMethod]
    public void Readback_respects_both_row_pitches()
    {
        var source = new byte[] { 1, 2, 3, 4, 99, 99, 99, 99, 5, 6, 7, 8, 99, 99, 99, 99 };
        var destination = new byte[12];
        var from = GCHandle.Alloc(source, GCHandleType.Pinned);
        try
        {
            CaptureInterop.CopyRows(from.AddrOfPinnedObject(), 8, destination, 6, 1, 2);
            Assert.AreSequenceEqual(new byte[] { 1, 2, 3, 4, 0, 0, 5, 6, 7, 8, 0, 0 }, destination);
        }
        finally
        {
            from.Free();
        }
    }

    private sealed class FakeCapture : ICaptureBackend
    {
        public CaptureTarget? Target { get; private set; }

        public Action? Closed { get; private set; }

        public bool Cursor { get; private set; }

        public int Starts { get; private set; }

        public int Reads { get; private set; }

        public int Disposals { get; private set; }

        public PixelImage? Next { get; set; }

        public bool FailStart { get; init; }

        public bool FailRead { get; init; }

        public bool FailDispose { get; init; }

        public void Start(CaptureTarget target, bool includeCursor, Action closed)
        {
            Starts++;
            Target = target;
            Cursor = includeCursor;
            Closed = closed;
            if (FailStart)
                throw new UnauthorizedAccessException("Capture denied by Windows policy.");
        }

        public PixelImage? ReadLatest()
        {
            Reads++;
            if (FailRead)
                throw new COMException("Device removed.");
            var next = Next;
            Next = null;
            return next;
        }

        public void Dispose()
        {
            Disposals++;
            if (FailDispose)
                throw new COMException("Device removed during teardown.");
        }
    }
}
