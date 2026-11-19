using System.Diagnostics;
using System.Globalization;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Tests;

[Collection(ExplorerTaskbarCollection.Name)]
public sealed class HiddenTaskbarWorkAreaTests : IDisposable, IClassFixture<ProbeBuild>
{
    private readonly string _dir = TempFolder.NewPath();
    private readonly List<Process> _started = [];
    private readonly uint _state = AppBars.State();
    private bool _restore;

    [Fact]
    [Trait("Category", "Window")]
    public void Hiding_the_taskbar_returns_every_monitor_and_quitting_puts_it_back()
    {
        var (windows, _) = ExplorerTaskbarWindow.Find();
        Assert.NotEmpty(windows);
        Assert.All(windows, pair => Assert.True(PInvoke.IsWindowVisible((HWND)pair.Hwnd),
            "Explorer's taskbar was hidden before the test."));
        var before = Areas();
        _restore = true;
        var config = Folder("config");
        var local = Folder("local");
        File.WriteAllText(Path.Combine(config, ProbeShell.HideTaskbarMarker), "");
        var shell = Start(config, local);

        Wait.Until(Reclaimed, "every monitor's work area to drop Explorer's taskbar");
        Wait.ForNothingToHappen(TimeSpan.FromSeconds(2));
        Assert.True(Reclaimed(), Describe(Areas()));

        var stopped = WinshellCli.Run(["stop", "--pid", shell.Id.ToString(CultureInfo.InvariantCulture)],
            IsolatedFolders.Of(config, local));
        stopped.AssertExitCode(0);
        Wait.Until(() => shell.HasExited && Restored(before), "quitting to put the taskbar and the work area back");
        Wait.ForNothingToHappen(TimeSpan.FromMilliseconds(500));
        Assert.True(Restored(before), Describe(Areas()));
        Assert.Equal(_state, AppBars.State());
        _restore = false;
    }

    private static bool Reclaimed()
    {
        var (windows, _) = ExplorerTaskbarWindow.Find();
        return windows.Count > 0 && windows.All(pair => !PInvoke.IsWindowVisible((HWND)pair.Hwnd)) &&
               Areas().Values.All(area => area.Left == area.WorkLeft && area.Top == area.WorkTop &&
                                          area.Right == area.WorkRight && area.Bottom == area.WorkBottom);
    }

    private static bool Restored(Dictionary<string, Area> before)
    {
        var (windows, _) = ExplorerTaskbarWindow.Find();
        if (windows.Count == 0 || windows.Any(pair => !PInvoke.IsWindowVisible((HWND)pair.Hwnd)))
            return false;
        var now = Areas();
        if (now.Count != before.Count)
            return false;
        foreach (var (device, area) in before)
        {
            if (!now.TryGetValue(device, out var other) || area != other)
                return false;
        }

        return true;
    }

    private static Dictionary<string, Area> Areas()
    {
        var areas = new Dictionary<string, Area>();
        foreach (var handle in Monitors.List())
        {
            Assert.True(Monitors.TryRead(handle, out var info));
            areas[info.Device] = new Area(info.Bounds.left, info.Bounds.top, info.Bounds.right, info.Bounds.bottom,
                info.Work.left, info.Work.top, info.Work.right, info.Work.bottom);
        }

        return areas;
    }

    private static string Describe(Dictionary<string, Area> areas)
    {
        return string.Join("; ", areas.Select(pair =>
            $"{pair.Key} bounds {pair.Value.Left},{pair.Value.Top},{pair.Value.Right},{pair.Value.Bottom} work {pair.Value.WorkLeft},{pair.Value.WorkTop},{pair.Value.WorkRight},{pair.Value.WorkBottom}"));
    }

    private string Folder(string name)
    {
        var path = Path.Combine(_dir, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private Process Start(string config, string local)
    {
        var process = ProbeShell.Start(config, local);
        _started.Add(process);
        return process;
    }

    public void Dispose()
    {
        foreach (var process in _started)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(true);
            }
            catch (Exception)
            {
            }

            process.Dispose();
        }

        if (_restore)
        {
            foreach (var (hwnd, _) in ExplorerTaskbarWindow.Find().Windows)
                PInvoke.ShowWindow((HWND)hwnd, SHOW_WINDOW_CMD.SW_SHOWNA);
            AppBars.SetState(_state);
        }

        try
        {
            Directory.Delete(_dir, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private readonly record struct Area(int Left, int Top, int Right, int Bottom, int WorkLeft, int WorkTop,
        int WorkRight, int WorkBottom);
}
