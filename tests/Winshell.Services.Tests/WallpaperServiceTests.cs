using Winshell.Wallpaper;

namespace Winshell.Services.Tests;

public sealed class WallpaperServiceTests
{
    private static readonly TimeSpan Settled = TimeSpan.FromSeconds(1);

    [Fact]
    public void Each_screen_has_its_image_and_the_desktop_has_one_fit_and_color()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeWallpaperBackend(Snapshot());
        using var wallpaper = new WallpaperService(loop, new Log(), backend);

        Assert.Equal(2, wallpaper.Screens.Count);
        Assert.Equal(@"\\.\DISPLAY1", wallpaper.Screens[0].Id);
        Assert.Equal(@"C:\wall\a.jpg", wallpaper.Screens[0].Image);
        Assert.True(wallpaper.Screens[0].IsPrimary);
        Assert.Equal(@"\\.\DISPLAY2", wallpaper.Screens[1].Id);
        Assert.Equal(@"C:\wall\b.jpg", wallpaper.Screens[1].Image);
        Assert.False(wallpaper.Screens[1].IsPrimary);
        Assert.Equal(WallpaperFit.Fill, wallpaper.Fit);
        Assert.Equal(new WallpaperColor(1, 2, 3), wallpaper.Background);
        Assert.Equal(1, backend.Reads);
        Assert.Null(loop.NextDue);
    }

    [Fact]
    public void An_image_can_be_set_on_one_screen_or_on_every_screen()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeWallpaperBackend(Snapshot());
        using var wallpaper = new WallpaperService(loop, new Log(), backend);
        List<string?> heard = [];
        wallpaper.PropertyChanged += (_, e) => heard.Add(e.PropertyName);
        var left = wallpaper.Screens[0];
        var right = wallpaper.Screens[1];

        Assert.False(wallpaper.SetImage(left, @"C:\wall\a.jpg"));
        Assert.Empty(backend.Images);

        Assert.True(wallpaper.SetImage(left, @"C:\wall\c.jpg"));
        Assert.Equal([(@"\\?\DISPLAY#LEFT", @"C:\wall\c.jpg")], backend.Images);
        Assert.Equal(@"C:\wall\c.jpg", wallpaper.Screens[0].Image);
        Assert.Equal(@"C:\wall\b.jpg", wallpaper.Screens[1].Image);
        Assert.Equal([nameof(WallpaperService.Screens)], heard);

        heard.Clear();
        Assert.True(wallpaper.SetImage(@"C:\wall\d.jpg"));
        Assert.Equal((null, @"C:\wall\d.jpg"), backend.Images[^1]);
        Assert.Equal(@"C:\wall\d.jpg", wallpaper.Screens[0].Image);
        Assert.Equal(@"C:\wall\d.jpg", wallpaper.Screens[1].Image);
        Assert.Equal([nameof(WallpaperService.Screens)], heard);
        Assert.False(wallpaper.SetImage(@"c:\wall\D.JPG"));
        Assert.Equal(2, backend.Images.Count);

        heard.Clear();
        Assert.False(wallpaper.SetFit(WallpaperFit.Fill));
        Assert.True(wallpaper.SetFit(WallpaperFit.Span));
        Assert.Equal([WallpaperFit.Span], backend.Fits);
        Assert.Equal(WallpaperFit.Span, wallpaper.Fit);
        Assert.Equal([nameof(WallpaperService.Fit)], heard);

        heard.Clear();
        Assert.False(wallpaper.SetBackground(new WallpaperColor(1, 2, 3)));
        Assert.True(wallpaper.SetBackground(new WallpaperColor(4, 5, 6)));
        Assert.Equal([new WallpaperColor(4, 5, 6)], backend.Colors);
        Assert.Equal(new WallpaperColor(4, 5, 6), wallpaper.Background);
        Assert.Equal([nameof(WallpaperService.Background)], heard);
        Assert.Null(loop.NextDue);
    }

    [Fact]
    public void A_change_is_reported_on_the_loop_once_Windows_has_finished_writing()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeWallpaperBackend(Snapshot());
        using var wallpaper = new WallpaperService(loop, new Log(), backend);
        List<string?> heard = [];
        wallpaper.PropertyChanged += (_, e) => heard.Add(e.PropertyName);
        var reads = backend.Reads;

        backend.Snapshot = backend.Snapshot with { Fit = WallpaperFit.Tile };
        backend.Raise();
        loop.Process();
        Assert.Equal(WallpaperFit.Fill, wallpaper.Fit);
        Assert.Empty(heard);

        backend.Raise();
        loop.Advance(Settled);
        Assert.Equal(WallpaperFit.Tile, wallpaper.Fit);
        Assert.Equal([nameof(WallpaperService.Fit)], heard);
        Assert.Equal(reads + 1, backend.Reads);

        heard.Clear();
        backend.Snapshot = backend.Snapshot with
        {
            Monitors =
            [
                backend.Snapshot.Monitors[0] with { Image = @"C:\wall\e.jpg" },
                backend.Snapshot.Monitors[1]
            ],
            Background = new WallpaperColor(9, 9, 9)
        };
        backend.Raise();
        loop.Advance(Settled);
        Assert.Equal(@"C:\wall\e.jpg", wallpaper.Screens[0].Image);
        Assert.Equal(@"C:\wall\b.jpg", wallpaper.Screens[1].Image);
        Assert.Equal(new WallpaperColor(9, 9, 9), wallpaper.Background);
        Assert.Equal([nameof(WallpaperService.Screens), nameof(WallpaperService.Background)], heard);

        heard.Clear();
        var after = backend.Reads;
        backend.Raise();
        loop.Advance(Settled);
        Assert.Empty(heard);
        Assert.Equal(after + 1, backend.Reads);
        Assert.Null(loop.NextDue);
    }

    [Fact]
    public void A_set_is_ignored_for_an_unknown_screen_the_current_value_or_a_disposed_service()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeWallpaperBackend(Snapshot());
        var wallpaper = new WallpaperService(loop, new Log(), backend);
        var unknown = new WallpaperScreen(@"\\.\DISPLAY9", @"\\?\DISPLAY#NO", @"C:\wall\z.jpg", false);

        Assert.False(wallpaper.SetImage(unknown, @"C:\wall\z.jpg"));
        Assert.False(wallpaper.SetImage(wallpaper.Screens[0], "  "));
        Assert.False(wallpaper.SetFit((WallpaperFit)99));
        Assert.Throws<ArgumentNullException>(() => wallpaper.SetImage(null!, @"C:\wall\z.jpg"));
        Assert.Throws<ArgumentNullException>(() => wallpaper.SetImage(null!));
        Assert.Empty(backend.Images);
        Assert.Empty(backend.Fits);

        backend.Accepts = false;
        Assert.False(wallpaper.SetImage(wallpaper.Screens[0], @"C:\wall\c.jpg"));
        Assert.False(wallpaper.SetFit(WallpaperFit.Center));
        Assert.False(wallpaper.SetBackground(new WallpaperColor(7, 7, 7)));
        Assert.Equal(@"C:\wall\a.jpg", wallpaper.Screens[0].Image);
        Assert.Equal(WallpaperFit.Fill, wallpaper.Fit);
        Assert.Equal(new WallpaperColor(1, 2, 3), wallpaper.Background);

        var images = backend.Images.Count;
        var fits = backend.Fits.Count;
        var colors = backend.Colors.Count;
        wallpaper.Dispose();
        backend.Accepts = true;
        Assert.False(wallpaper.SetImage(@"C:\wall\c.jpg"));
        Assert.False(wallpaper.SetFit(WallpaperFit.Center));
        Assert.False(wallpaper.SetBackground(new WallpaperColor(7, 7, 7)));
        Assert.Equal(images, backend.Images.Count);
        Assert.Equal(fits, backend.Fits.Count);
        Assert.Equal(colors, backend.Colors.Count);
        backend.Snapshot = backend.Snapshot with { Fit = WallpaperFit.Span };
        backend.Raise();
        loop.Advance(Settled);
        Assert.Equal(WallpaperFit.Fill, wallpaper.Fit);
        Assert.Null(loop.NextDue);
    }

    [Fact]
    public void Until_a_reading_succeeds_a_set_is_sent_and_a_later_miss_keeps_the_last_one()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeWallpaperBackend(WallpaperSnapshot.Unavailable);
        using var wallpaper = new WallpaperService(loop, new Log(), backend);

        Assert.Empty(wallpaper.Screens);
        Assert.Equal(WallpaperFit.Fill, wallpaper.Fit);
        Assert.Equal(new WallpaperColor(0, 0, 0), wallpaper.Background);
        Assert.True(wallpaper.SetFit(WallpaperFit.Fill));
        Assert.True(wallpaper.SetBackground(new WallpaperColor(0, 0, 0)));
        Assert.Equal([WallpaperFit.Fill], backend.Fits);
        Assert.Equal([new WallpaperColor(0, 0, 0)], backend.Colors);

        backend.Snapshot = Snapshot();
        backend.Raise();
        loop.Advance(Settled);
        Assert.Equal(@"C:\wall\a.jpg", wallpaper.Screens[0].Image);
        Assert.Equal(new WallpaperColor(1, 2, 3), wallpaper.Background);
        Assert.False(wallpaper.SetFit(WallpaperFit.Fill));
        Assert.False(wallpaper.SetBackground(new WallpaperColor(1, 2, 3)));
        Assert.Single(backend.Fits);
        Assert.Single(backend.Colors);

        List<string?> heard = [];
        wallpaper.PropertyChanged += (_, e) => heard.Add(e.PropertyName);
        backend.Snapshot = WallpaperSnapshot.Unavailable;
        backend.Raise();
        loop.Advance(Settled);
        Assert.Equal(@"C:\wall\a.jpg", wallpaper.Screens[0].Image);
        Assert.Equal(WallpaperFit.Fill, wallpaper.Fit);
        Assert.Equal(new WallpaperColor(1, 2, 3), wallpaper.Background);
        Assert.Empty(heard);
        Assert.Null(loop.NextDue);
    }

    private static WallpaperSnapshot Snapshot()
    {
        return new WallpaperSnapshot(true,
        [
            new WallpaperMonitor(@"\\.\DISPLAY1", @"\\?\DISPLAY#LEFT", @"C:\wall\a.jpg", true),
            new WallpaperMonitor(@"\\.\DISPLAY2", @"\\?\DISPLAY#RIGHT", @"C:\wall\b.jpg", false)
        ], WallpaperFit.Fill, new WallpaperColor(1, 2, 3));
    }

    private sealed class FakeWallpaperBackend(WallpaperSnapshot snapshot) : IWallpaperBackend
    {
        public WallpaperSnapshot Snapshot { get; set; } = snapshot;

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
            return Snapshot;
        }

        public bool SetImage(string? monitorId, string image)
        {
            Images.Add((monitorId, image));
            if (!Accepts)
                return false;
            Snapshot = Snapshot with
            {
                Monitors = Snapshot.Monitors.Select(monitor =>
                        monitorId is null || monitor.MonitorId == monitorId ? monitor with { Image = image } : monitor)
                    .ToArray()
            };
            return true;
        }

        public bool SetFit(WallpaperFit fit)
        {
            Fits.Add(fit);
            if (!Accepts)
                return false;
            Snapshot = Snapshot with { Fit = fit };
            return true;
        }

        public bool SetBackground(WallpaperColor color)
        {
            Colors.Add(color);
            if (!Accepts)
                return false;
            Snapshot = Snapshot with { Background = color };
            return true;
        }

        public void Dispose()
        {
        }
    }
}
