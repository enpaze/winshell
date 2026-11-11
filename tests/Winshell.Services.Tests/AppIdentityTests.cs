using Winshell.Apps;
using Winshell.Tray;
using Winshell.Windows;

namespace Winshell.Services.Tests;

/// <summary>A window, a tray icon and a Start entry name one program the same way.</summary>
public sealed class AppIdentityTests
{
    [Fact]
    public void A_window_a_tray_icon_and_a_Start_entry_share_one_app_id_and_icon_path()
    {
        var root = AppIndexTests.Temp();
        try
        {
            var program = Path.Combine(root, "Demo.EXE");
            var notes = Path.Combine(root, "Notes.txt");
            File.WriteAllBytes(program, []);
            File.WriteAllText(notes, "notes");
            var programs = Path.Combine(root, "Programs");
            var demoLink = Path.Combine(programs, "Demo.lnk");
            var notesLink = Path.Combine(programs, "Notes.lnk");
            AppIndexTests.WriteLink(demoLink, program, "", root, "Demo");
            AppIndexTests.WriteLink(notesLink, notes, "", "", "notes");

            var shortcuts = AppIndex.Shortcuts(programs);
            var demo = Publish(Assert.Single(shortcuts, app => app.Name == "Demo"));
            var document = Publish(Assert.Single(shortcuts, app => app.Name == "Notes"));

            const string packagedId = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
            var packaged = Publish(AppIndex.Packaged(packagedId, "Calculator", "")!);
            var windows = new FakeWindowBackend();
            windows.Show(1, FakeWindowBackend.Window() with { Executable = program });
            windows.Show(2, FakeWindowBackend.Window() with
            {
                AppUserModelId = packagedId,
                Executable = @"C:\Windows\System32\ApplicationFrameHost.exe"
            });
            using var list = new WindowListService(new ManualEventLoop(), Log.Default, backend: windows);
            var window = Assert.Single(list.Windows, item => item.Executable == program);
            var packagedWindow = Assert.Single(list.Windows, item => item.AppId == packagedId);

            const nint hwnd = 0x100;
            var loop = new ManualEventLoop();
            var trayBackend = new FakeTrayBackend();
            trayBackend.Windows[hwnd] = 42;
            trayBackend.Programs[hwnd] = program;
            using var tray = new SystemTray(loop, Log.Default, trayBackend);
            loop.Process();
            trayBackend.Send(new NotifyIconCall
            {
                Message = TrayProtocol.Add,
                Flags = TrayProtocol.NIF_TIP,
                Window = hwnd,
                Id = 1,
                Tip = "Demo"
            });
            var icon = Assert.Single(tray.Items);

            Assert.Equal("demo", demo.AppId);
            Assert.Equal("demo", window.AppId);
            Assert.Equal("demo", icon.Id);
            Assert.Equal("Demo", icon.AppName);
            Assert.Equal(program, demo.Icon);
            Assert.Equal(program, window.Icon);
            // A tray icon publishes no icon path. For a desktop program the executable is that path.
            Assert.Equal(program, icon.Executable);

            Assert.Equal(packagedId, packaged.AppId);
            Assert.Equal(packagedId, packagedWindow.AppId);
            Assert.Equal(@"shell:AppsFolder\" + packagedId, packaged.Icon);
            Assert.Equal(@"shell:AppsFolder\" + packagedId, packagedWindow.Icon);
            // A Start shortcut can carry a packaged id and no program. The icon still comes from that id.
            var carried = Publish(new AppRecord(
                "Calculator", "Calculator", "", notesLink, "", "", "", packagedId, "", false, false));
            Assert.Equal(packagedId, carried.AppId);
            Assert.Equal(@"shell:AppsFolder\" + packagedId, carried.Icon);

            Assert.Equal("", document.AppId);
            Assert.True(document.Hidden);
            Assert.Equal(notesLink, document.Icon);
        }
        finally
        {
            AppIndexTests.Delete(root);
        }
    }

    [Fact]
    public void A_packaged_app_icon_comes_from_Start_and_every_other_icon_from_the_program()
    {
        Assert.Equal(@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
            AppIdentity.IconPath("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
                @"C:\Windows\System32\ApplicationFrameHost.exe"));
        Assert.Equal(@"C:\Program Files\Google\Chrome\Application\chrome.exe",
            AppIdentity.IconPath("Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe"));
        Assert.Equal(@"C:\Program Files\App\app.exe", AppIdentity.IconPath("", @"C:\Program Files\App\app.exe"));
    }

    private static AppEntry Publish(AppRecord record)
    {
        return new AppEntry(record, default, new ManualEventLoop(), Log.Default, new QuietApps());
    }

    private sealed class QuietApps : IAppsBackend
    {
        public event Action? Changed { add { } remove { } }

        public void Start()
        {
        }

        public Task<AppSnapshot> ReadAsync()
        {
            return Task.FromResult(AppSnapshot.Empty);
        }

        public string? OpenShortcut(string path, string startIn)
        {
            return null;
        }

        public Task<string?> ActivateAsync(string appUserModelId, string arguments)
        {
            return Task.FromResult<string?>(null);
        }

        public string? OpenAppsFolder(string appUserModelId)
        {
            return null;
        }

        public void Dispose()
        {
        }
    }
}
