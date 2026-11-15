using System.Diagnostics;
using Winshell.ExplorerTaskbar;
using Winshell.Native;

namespace Winshell.Services.Tests;

/// <summary>
/// The taskbar lease is one file in a local folder. These tests use a folder of their own and never the process's,
/// and the window they record is not a taskbar, so putting it back changes nothing on screen.
/// </summary>
public sealed class ExplorerTaskbarLeaseTests : IDisposable
{
    private readonly string _dir = TempFolder.NewPath();

    private ShellLocations Locations => new(Path.Combine(_dir, "config"), Path.Combine(_dir, "local"));

    public ExplorerTaskbarLeaseTests()
    {
        Directory.CreateDirectory(Locations.LocalRoot);
    }

    [Fact]
    public void The_lease_remembers_the_windows_and_a_bad_file_is_no_lease()
    {
        var locations = Locations;
        var window = Window();

        Assert.True(ExplorerTaskbarLease.Write(locations, 4242, [window]));

        var read = Assert.Single(ExplorerTaskbarLease.Windows(locations));
        Assert.Equal((window.Hwnd, window.WasVisible, window.Reserves, window.Removed, window.Edge),
            (read.Hwnd, read.WasVisible, read.Reserves, read.Removed, read.Edge));
        Assert.Equal((window.Rect.left, window.Rect.top, window.Rect.right, window.Rect.bottom),
            (read.Rect.left, read.Rect.top, read.Rect.right, read.Rect.bottom));

        File.WriteAllText(Assert.Single(Directory.GetFiles(locations.LocalRoot, "*.json")), "{");
        Assert.Empty(ExplorerTaskbarLease.Windows(locations));
        Assert.True(ExplorerTaskbarLease.LeaveHidden(locations, 1, true));
        Assert.False(ExplorerTaskbarLease.LeaveHidden(locations, 1, false));
    }

    [Fact]
    public void A_crash_restores_a_lease_this_start_owns_and_leaves_one_another_shell_owns()
    {
        var locations = Locations;
        Assert.True(ExplorerTaskbarLease.LeaveHidden(locations, 1, true));
        Assert.False(ExplorerTaskbarLease.LeaveHidden(locations, 1, false));

        Assert.True(ExplorerTaskbarLease.Write(locations, 4242, [Window()]));
        Assert.False(ExplorerTaskbarLease.LeaveHidden(locations, 4242, true));

        using var gone = Process.Start(new ProcessStartInfo(ComSpec(), "/d /c exit")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        Wait.Until(() => gone.HasExited, "the owner to exit");
        Assert.True(ExplorerTaskbarLease.Write(locations, gone.Id, [Window()]));
        Assert.False(ExplorerTaskbarLease.LeaveHidden(locations, 1, true));

        using var owner = Process.Start(new ProcessStartInfo(ComSpec(), "/d /c ping -n 30 127.0.0.1")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        try
        {
            Assert.True(ExplorerTaskbarLease.Write(locations, owner.Id, [Window()]));
            Assert.True(ExplorerTaskbarLease.LeaveHidden(locations, 1, false));
            ExplorerTaskbarLease.ReleaseUnclaimed(locations);
            Assert.True(ExplorerTaskbarLease.Exists(locations));

            ExplorerTaskbarLease.HandOff(locations);
            Assert.True(ExplorerTaskbarLease.LeaveHidden(locations, owner.Id, false));
            ExplorerTaskbarLease.ReleaseUnclaimed(locations);
            Assert.False(ExplorerTaskbarLease.Exists(locations));
        }
        finally
        {
            if (!owner.HasExited)
                owner.Kill(true);
        }
    }

    private static ExplorerTaskbarWindow.Saved Window()
    {
        return new ExplorerTaskbarWindow.Saved
        {
            Hwnd = 42,
            WasVisible = true,
            Reserves = true,
            Removed = true,
            Edge = AppBarEdge.Bottom,
            Rect = new RECT { left = 0, top = 1000, right = 800, bottom = 1040 }
        };
    }

    private static string ComSpec()
    {
        return Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
