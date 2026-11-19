using Winshell.Wallpaper;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class WallpaperServiceTests
{
    private static readonly TimeSpan Settled = TimeSpan.FromSeconds(1);

    [TestMethod]
    public void Each_screen_has_its_image_and_the_desktop_has_one_fit_and_color()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeWallpaperBackend(Snapshot());
        var wallpaper = new WallpaperService(loop, new Log(), backend);
        using var stopWallpaper = wallpaper.Stopping();

        Assert.AreEqual(2, wallpaper.Screens.Count);
        Assert.AreEqual(@"\\.\DISPLAY1", wallpaper.Screens[0].Id);
        Assert.AreEqual(@"C:\wall\a.jpg", wallpaper.Screens[0].Image);
        Assert.IsTrue(wallpaper.Screens[0].IsPrimary);
        Assert.AreEqual(@"\\.\DISPLAY2", wallpaper.Screens[1].Id);
        Assert.AreEqual(@"C:\wall\b.jpg", wallpaper.Screens[1].Image);
        Assert.IsFalse(wallpaper.Screens[1].IsPrimary);
        Assert.AreEqual(WallpaperFit.Fill, wallpaper.Fit);
        Assert.AreEqual(new WallpaperColor(1, 2, 3), wallpaper.Background);
        Assert.AreEqual(1, backend.Reads);
        Assert.IsNull(loop.NextDue);
    }

    [TestMethod]
    public void An_image_can_be_set_on_one_screen_or_on_every_screen()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeWallpaperBackend(Snapshot());
        var wallpaper = new WallpaperService(loop, new Log(), backend);
        using var stopWallpaper = wallpaper.Stopping();
        List<string?> heard = [];
        wallpaper.PropertyChanged += (_, e) => heard.Add(e.PropertyName);
        var left = wallpaper.Screens[0];

        Assert.IsFalse(wallpaper.SetImage(left, @"C:\wall\a.jpg"));
        Assert.IsEmpty(backend.Images);

        Assert.IsTrue(wallpaper.SetImage(left, @"C:\wall\c.jpg"));
        Assert.AreSequenceEqual([(@"\\?\DISPLAY#LEFT", @"C:\wall\c.jpg")], backend.Images);
        Assert.AreEqual(@"C:\wall\c.jpg", wallpaper.Screens[0].Image);
        Assert.AreEqual(@"C:\wall\b.jpg", wallpaper.Screens[1].Image);
        Assert.AreSequenceEqual([nameof(WallpaperService.Screens)], heard);

        heard.Clear();
        Assert.IsTrue(wallpaper.SetImage(@"C:\wall\d.jpg"));
        Assert.AreEqual((null, @"C:\wall\d.jpg"), backend.Images[^1]);
        Assert.AreEqual(@"C:\wall\d.jpg", wallpaper.Screens[0].Image);
        Assert.AreEqual(@"C:\wall\d.jpg", wallpaper.Screens[1].Image);
        Assert.AreSequenceEqual([nameof(WallpaperService.Screens)], heard);
        Assert.IsFalse(wallpaper.SetImage(@"c:\wall\D.JPG"));
        Assert.AreEqual(2, backend.Images.Count);

        heard.Clear();
        Assert.IsFalse(wallpaper.SetFit(WallpaperFit.Fill));
        Assert.IsTrue(wallpaper.SetFit(WallpaperFit.Span));
        Assert.AreSequenceEqual([WallpaperFit.Span], backend.Fits);
        Assert.AreEqual(WallpaperFit.Span, wallpaper.Fit);
        Assert.AreSequenceEqual([nameof(WallpaperService.Fit)], heard);

        heard.Clear();
        Assert.IsFalse(wallpaper.SetBackground(new WallpaperColor(1, 2, 3)));
        Assert.IsTrue(wallpaper.SetBackground(new WallpaperColor(4, 5, 6)));
        Assert.AreSequenceEqual([new WallpaperColor(4, 5, 6)], backend.Colors);
        Assert.AreEqual(new WallpaperColor(4, 5, 6), wallpaper.Background);
        Assert.AreSequenceEqual([nameof(WallpaperService.Background)], heard);
        Assert.IsNull(loop.NextDue);
    }

    [TestMethod]
    public void A_change_is_reported_on_the_loop_once_Windows_has_finished_writing()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeWallpaperBackend(Snapshot());
        var wallpaper = new WallpaperService(loop, new Log(), backend);
        using var stopWallpaper = wallpaper.Stopping();
        List<string?> heard = [];
        wallpaper.PropertyChanged += (_, e) => heard.Add(e.PropertyName);
        var reads = backend.Reads;

        backend.Current = backend.Current with { Fit = WallpaperFit.Tile };
        backend.Raise();
        loop.Process();
        Assert.AreEqual(WallpaperFit.Fill, wallpaper.Fit);
        Assert.IsEmpty(heard);

        backend.Raise();
        loop.Advance(Settled);
        Assert.AreEqual(WallpaperFit.Tile, wallpaper.Fit);
        Assert.AreSequenceEqual([nameof(WallpaperService.Fit)], heard);
        Assert.AreEqual(reads + 1, backend.Reads);

        heard.Clear();
        backend.Current = backend.Current with
        {
            Screens =
            [
                WithImage(backend.Current.Screens[0], @"C:\wall\e.jpg"),
                backend.Current.Screens[1]
            ],
            Background = new WallpaperColor(9, 9, 9)
        };
        backend.Raise();
        loop.Advance(Settled);
        Assert.AreEqual(@"C:\wall\e.jpg", wallpaper.Screens[0].Image);
        Assert.AreEqual(@"C:\wall\b.jpg", wallpaper.Screens[1].Image);
        Assert.AreEqual(new WallpaperColor(9, 9, 9), wallpaper.Background);
        Assert.AreSequenceEqual([nameof(WallpaperService.Screens), nameof(WallpaperService.Background)], heard);

        heard.Clear();
        var after = backend.Reads;
        backend.Raise();
        loop.Advance(Settled);
        Assert.IsEmpty(heard);
        Assert.AreEqual(after + 1, backend.Reads);
        Assert.IsNull(loop.NextDue);
    }

    [TestMethod]
    public void A_set_is_ignored_for_an_unknown_screen_the_current_value_or_a_disposed_service()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeWallpaperBackend(Snapshot());
        var wallpaper = new WallpaperService(loop, new Log(), backend);
        var unknown = new WallpaperScreen(@"\\.\DISPLAY9", @"\\?\DISPLAY#NO", @"C:\wall\z.jpg", false);

        Assert.IsFalse(wallpaper.SetImage(unknown, @"C:\wall\z.jpg"));
        Assert.IsFalse(wallpaper.SetImage(wallpaper.Screens[0], "  "));
        Assert.IsFalse(wallpaper.SetFit((WallpaperFit)99));
        Assert.ThrowsExactly<ArgumentNullException>(() => wallpaper.SetImage(null!, @"C:\wall\z.jpg"));
        Assert.ThrowsExactly<ArgumentNullException>(() => wallpaper.SetImage(null!));
        Assert.IsEmpty(backend.Images);
        Assert.IsEmpty(backend.Fits);

        backend.Accepts = false;
        Assert.IsFalse(wallpaper.SetImage(wallpaper.Screens[0], @"C:\wall\c.jpg"));
        Assert.IsFalse(wallpaper.SetFit(WallpaperFit.Center));
        Assert.IsFalse(wallpaper.SetBackground(new WallpaperColor(7, 7, 7)));
        Assert.AreEqual(@"C:\wall\a.jpg", wallpaper.Screens[0].Image);
        Assert.AreEqual(WallpaperFit.Fill, wallpaper.Fit);
        Assert.AreEqual(new WallpaperColor(1, 2, 3), wallpaper.Background);

        var images = backend.Images.Count;
        var fits = backend.Fits.Count;
        var colors = backend.Colors.Count;
        wallpaper.Stop();
        backend.Accepts = true;
        Assert.IsFalse(wallpaper.SetImage(@"C:\wall\c.jpg"));
        Assert.IsFalse(wallpaper.SetFit(WallpaperFit.Center));
        Assert.IsFalse(wallpaper.SetBackground(new WallpaperColor(7, 7, 7)));
        Assert.AreEqual(images, backend.Images.Count);
        Assert.AreEqual(fits, backend.Fits.Count);
        Assert.AreEqual(colors, backend.Colors.Count);
        backend.Current = backend.Current with { Fit = WallpaperFit.Span };
        backend.Raise();
        loop.Advance(Settled);
        Assert.AreEqual(WallpaperFit.Fill, wallpaper.Fit);
        Assert.IsNull(loop.NextDue);
    }

    [TestMethod]
    public void Until_a_reading_succeeds_a_set_is_sent_and_a_later_miss_keeps_the_last_one()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeWallpaperBackend(WallpaperSnapshot.Unavailable);
        var wallpaper = new WallpaperService(loop, new Log(), backend);
        using var stopWallpaper = wallpaper.Stopping();

        Assert.IsEmpty(wallpaper.Screens);
        Assert.AreEqual(WallpaperFit.Fill, wallpaper.Fit);
        Assert.AreEqual(new WallpaperColor(0, 0, 0), wallpaper.Background);
        Assert.IsTrue(wallpaper.SetFit(WallpaperFit.Fill));
        Assert.IsTrue(wallpaper.SetBackground(new WallpaperColor(0, 0, 0)));
        Assert.AreSequenceEqual([WallpaperFit.Fill], backend.Fits);
        Assert.AreSequenceEqual([new WallpaperColor(0, 0, 0)], backend.Colors);

        backend.Current = Snapshot();
        backend.Raise();
        loop.Advance(Settled);
        Assert.AreEqual(@"C:\wall\a.jpg", wallpaper.Screens[0].Image);
        Assert.AreEqual(new WallpaperColor(1, 2, 3), wallpaper.Background);
        Assert.IsFalse(wallpaper.SetFit(WallpaperFit.Fill));
        Assert.IsFalse(wallpaper.SetBackground(new WallpaperColor(1, 2, 3)));
        Assert.ContainsSingle(backend.Fits);
        Assert.ContainsSingle(backend.Colors);

        List<string?> heard = [];
        wallpaper.PropertyChanged += (_, e) => heard.Add(e.PropertyName);
        backend.Current = WallpaperSnapshot.Unavailable;
        backend.Raise();
        loop.Advance(Settled);
        Assert.AreEqual(@"C:\wall\a.jpg", wallpaper.Screens[0].Image);
        Assert.AreEqual(WallpaperFit.Fill, wallpaper.Fit);
        Assert.AreEqual(new WallpaperColor(1, 2, 3), wallpaper.Background);
        Assert.IsEmpty(heard);
        Assert.IsNull(loop.NextDue);
    }

    private static WallpaperSnapshot Snapshot()
    {
        return new WallpaperSnapshot(true,
        [
            new WallpaperScreen(@"\\.\DISPLAY1", @"\\?\DISPLAY#LEFT", @"C:\wall\a.jpg", true),
            new WallpaperScreen(@"\\.\DISPLAY2", @"\\?\DISPLAY#RIGHT", @"C:\wall\b.jpg", false)
        ], WallpaperFit.Fill, new WallpaperColor(1, 2, 3));
    }

    private static WallpaperScreen WithImage(WallpaperScreen screen, string image)
    {
        return new WallpaperScreen(screen.Id, screen.MonitorId, image, screen.IsPrimary);
    }

    private sealed class FakeWallpaperBackend(WallpaperSnapshot snapshot) : IWallpaperBackend
    {
        public WallpaperSnapshot Current { get; set; } = snapshot;

        public int Reads { get; private set; }

        public List<(string? Monitor, string Image)> Images { get; } = [];

        public List<WallpaperFit> Fits { get; } = [];

        public List<WallpaperColor> Colors { get; } = [];

        public bool Accepts { get; set; } = true;

        public event Action? Changed;

        public void Raise()
        {
            Changed?.Invoke();
        }

        public WallpaperSnapshot Read()
        {
            Reads++;
            return Current;
        }

        public bool SetImage(string? monitorId, string image)
        {
            Images.Add((monitorId, image));
            if (!Accepts)
                return false;
            Current = Current with
            {
                Screens = Current.Screens.Select(screen =>
                        monitorId is null || screen.MonitorId == monitorId ? WithImage(screen, image) : screen)
                    .ToArray()
            };
            return true;
        }

        public bool SetFit(WallpaperFit fit)
        {
            Fits.Add(fit);
            if (!Accepts)
                return false;
            Current = Current with { Fit = fit };
            return true;
        }

        public bool SetBackground(WallpaperColor color)
        {
            Colors.Add(color);
            if (!Accepts)
                return false;
            Current = Current with { Background = color };
            return true;
        }

        public void Dispose()
        {
        }
    }
}
