using Winshell.Apps;
using Winshell.Tray;
using Winshell.Windows;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class AppIdentityTests
{
    [TestMethod]
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
            var demo = Publish(Assert.ContainsSingle(app => app.Name == "Demo", shortcuts));
            var document = Publish(Assert.ContainsSingle(app => app.Name == "Notes", shortcuts));

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
            var window = Assert.ContainsSingle(item => item.Executable == program, list.Windows);
            var packagedWindow = Assert.ContainsSingle(item => item.AppId == packagedId, list.Windows);

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
            var icon = Assert.ContainsSingle(tray.Items);

            Assert.AreEqual("demo", demo.AppId);
            Assert.AreEqual("demo", window.AppId);
            Assert.AreEqual("demo", icon.Id);
            Assert.AreEqual("Demo", icon.AppName);
            Assert.AreEqual(program, demo.Icon);
            Assert.AreEqual(program, window.Icon);
            Assert.AreEqual(program, icon.Executable);

            Assert.AreEqual(packagedId, packaged.AppId);
            Assert.AreEqual(packagedId, packagedWindow.AppId);
            Assert.AreEqual(@"shell:AppsFolder\" + packagedId, packaged.Icon);
            Assert.AreEqual(@"shell:AppsFolder\" + packagedId, packagedWindow.Icon);
            var carried = Publish(new AppRecord(
                "Calculator", "Calculator", "", notesLink, "", "", "", packagedId, "", false, false));
            Assert.AreEqual(packagedId, carried.AppId);
            Assert.AreEqual(@"shell:AppsFolder\" + packagedId, carried.Icon);

            Assert.AreEqual("", document.AppId);
            Assert.IsTrue(document.Hidden);
            Assert.AreEqual(notesLink, document.Icon);
        }
        finally
        {
            AppIndexTests.Delete(root);
        }
    }

    [TestMethod]
    public void A_packaged_app_icon_comes_from_Start_and_every_other_icon_from_the_program()
    {
        Assert.AreEqual(@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
            AppIdentity.IconPath("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
                @"C:\Windows\System32\ApplicationFrameHost.exe"));
        Assert.AreEqual(@"C:\Program Files\Google\Chrome\Application\chrome.exe",
            AppIdentity.IconPath("Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe"));
        Assert.AreEqual(@"C:\Program Files\App\app.exe", AppIdentity.IconPath("", @"C:\Program Files\App\app.exe"));
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
