using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Winshell.Capture;

namespace Winshell.ShellTests;

/// <summary>The view shows a session's frames and lets go of it. What the screen then looks like is not asserted here.</summary>
[Trait("Category", "Window")]
public sealed partial class CaptureViewTests
{
    [Fact]
    public async Task A_capture_view_shows_the_frame_and_lets_go_when_it_leaves()
    {
        var kept = await ShellFixture.On(async shell =>
        {
            var screen = shell.Screens[0].Geometry;
            var backend = new FakeCapture();
            var loop = new ManualEventLoop();
            using var session = new CaptureSession(loop, () => backend);
            session.FrameInterval = 16;
            session.Target = CaptureTarget.ForWindow(1);
            var view = new CaptureView
            {
                Width = 80,
                Height = 48,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Session = session
            };
            using var popup = new PopupWindow();
            popup.Rect = new Rect(screen.X + 40, screen.Y + 40, 200, 120);
            popup.Content = new Grid { Children = { view } };
            popup.Visible = true;
            await ShellFixture.LaidOut(view);

            var red = Solid(255, 0, 0);
            backend.Next = red;
            Assert.True(session.Start());
            loop.Advance(TimeSpan.FromMilliseconds(16));
            var shown = Pixels(Shown(view));
            Assert.Equal(red.Bgra.ToArray(), shown);

            session.Stop();
            Assert.Null(ImageOf(view).Source);

            backend.Next = red;
            Assert.True(session.Start());
            loop.Advance(TimeSpan.FromMilliseconds(16));
            var bitmap = Shown(view);
            var before = Pixels(bitmap);

            var unloaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            view.Unloaded += (_, _) => unloaded.TrySetResult();
            popup.Content = new Grid();
            await unloaded.Task.WaitAsync(TestContext.Current.CancellationToken);

            var blue = Solid(0, 0, 255);
            backend.Next = blue;
            loop.Advance(TimeSpan.FromMilliseconds(16));
            Assert.Equal(before, Pixels(bitmap));
            Assert.Null(ImageOf(view).Source);
            Assert.Equal(blue, session.Frame);
            return true;
        });

        Assert.True(kept);
    }

    [Fact]
    public async Task A_capture_view_still_shows_a_frame_after_a_collection()
    {
        var shown = await ShellFixture.On(async shell =>
        {
            var screen = shell.Screens[0].Geometry;
            var backend = new FakeCapture();
            var loop = new ManualEventLoop();
            using var session = new CaptureSession(loop, () => backend);
            session.FrameInterval = 16;
            session.Target = CaptureTarget.ForWindow(1);
            var view = new CaptureView
            {
                Width = 80,
                Height = 48,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Session = session
            };
            using var popup = new PopupWindow();
            popup.Rect = new Rect(screen.X + 80, screen.Y + 80, 200, 120);
            popup.Content = new Grid { Children = { view } };
            popup.Visible = true;
            await ShellFixture.LaidOut(view);

            // The wrapper of a XamlRoot does not live as long as the native object it wraps.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var found = ReferenceEquals(ShellWindow.Of(view), popup);
            Assert.Null(ShellWindow.Of(new Grid()));
            var red = Solid(255, 0, 0);
            backend.Next = red;
            Assert.True(session.Start());
            loop.Advance(TimeSpan.FromMilliseconds(16));
            var pixels = Pixels(Shown(view));
            return (found, same: pixels.AsSpan().SequenceEqual(red.Bgra));
        });

        Assert.True(shown.found);
        Assert.True(shown.same);
    }

    [Fact]
    public async Task A_capture_view_shows_a_straight_frame_in_premultiplied_colors()
    {
        var shown = await ShellFixture.On(async shell =>
        {
            var screen = shell.Screens[0].Geometry;
            var backend = new FakeCapture();
            var loop = new ManualEventLoop();
            using var session = new CaptureSession(loop, () => backend);
            session.FrameInterval = 16;
            session.Target = CaptureTarget.ForWindow(1);
            var view = new CaptureView
            {
                Width = 80,
                Height = 48,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Session = session
            };
            using var popup = new PopupWindow();
            popup.Rect = new Rect(screen.X + 120, screen.Y + 40, 200, 120);
            popup.Content = new Grid { Children = { view } };
            popup.Visible = true;
            await ShellFixture.LaidOut(view);

            // Straight BGRA. These bytes are the premultiplied colors of that picture.
            backend.Next = new PixelImage(2, 2,
                [32, 64, 128, 128, 10, 20, 30, 255, 100, 150, 200, 0, 128, 64, 32, 64],
                false);
            Assert.True(session.Start());
            loop.Advance(TimeSpan.FromMilliseconds(16));
            return Pixels(Shown(view));
        });

        Assert.Equal(new byte[] { 16, 32, 64, 128, 10, 20, 30, 255, 0, 0, 0, 0, 32, 16, 8, 64 }, shown);
    }

    private static PixelImage Solid(byte red, byte green, byte blue)
    {
        var bgra = new byte[4 * 2 * 4];
        for (var i = 0; i < bgra.Length; i += 4)
            (bgra[i], bgra[i + 1], bgra[i + 2], bgra[i + 3]) = (blue, green, red, 255);
        return new PixelImage(4, 2, bgra, true);
    }

    private static Image ImageOf(CaptureView view)
    {
        return Assert.IsType<Image>(VisualTreeHelper.GetChild(view, 0));
    }

    private static WriteableBitmap Shown(CaptureView view)
    {
        return Assert.IsType<WriteableBitmap>(ImageOf(view).Source);
    }

    private static byte[] Pixels(WriteableBitmap bitmap)
    {
        var bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        using var stream = bitmap.PixelBuffer.AsStream();
        Assert.Equal(bytes.Length, (int)stream.Length);
        stream.ReadExactly(bytes);
        return bytes;
    }

    // Not in TestSupport: Core.Tests compiles it and cannot see ICaptureBackend. CaptureSessionTests' fake also counts calls
    // and fails on demand.
    private sealed partial class FakeCapture : ICaptureBackend
    {
        public PixelImage? Next { get; set; }

        public void Start(CaptureTarget target, bool includeCursor, Action closed)
        {
        }

        public PixelImage? ReadLatest()
        {
            var next = Next;
            Next = null;
            return next;
        }

        public void Dispose()
        {
        }
    }
}
