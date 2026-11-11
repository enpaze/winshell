using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Winshell.Apps;
using Winshell.Native;
using Winshell.Tray;
using Winshell.Windows;

namespace Winshell.Services.Tests;

/// <summary>A window, a tray icon and a Start entry name one program the same way.</summary>
public sealed class AppIdentityTests
{
    [Fact]
    public void A_window_a_tray_icon_and_a_Start_entry_share_one_app_id_and_icon_path()
    {
        var root = Temp();
        try
        {
            var program = Path.Combine(root, "Demo.EXE");
            var notes = Path.Combine(root, "Notes.txt");
            File.WriteAllBytes(program, []);
            File.WriteAllText(notes, "notes");
            var programs = Path.Combine(root, "Programs");
            var demoLink = Path.Combine(programs, "Demo.lnk");
            var notesLink = Path.Combine(programs, "Notes.lnk");
            WriteLink(demoLink, program, "", root, "Demo");
            WriteLink(notesLink, notes, "", "", "notes");

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
            Assert.Equal(program, demo.Icon);
            Assert.Equal(program, window.Icon);
            Assert.Equal(program, icon.Executable);

            Assert.Equal(packagedId, packaged.AppId);
            Assert.Equal(packagedId, packagedWindow.AppId);
            Assert.Equal(@"shell:AppsFolder\" + packagedId, packaged.Icon);
            Assert.Equal(@"shell:AppsFolder\" + packagedId, packagedWindow.Icon);

            Assert.Equal("", document.AppId);
            Assert.True(document.Hidden);
            Assert.Equal(notesLink, document.Icon);
        }
        finally
        {
            Delete(root);
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

    private static void WriteLink(string path, string target, string arguments, string workingDirectory,
        string description)
    {
        object? link = null;
        try
        {
            link = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"))!)!;
            var shell = (IShellLinkW)link;
            var file = (IPersistFile)link;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            shell.SetPath(target);
            shell.SetArguments(arguments);
            shell.SetWorkingDirectory(workingDirectory);
            shell.SetDescription(description);
            file.Save(Path.GetFullPath(path), true);
        }
        finally
        {
            if (link is not null)
                Marshal.ReleaseComObject(link);
        }
    }

    private static string Temp()
    {
        var path = TempFolder.NewPath();
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Delete(string path)
    {
        Wait.Until(() => Deleted(path), $"{path} to be deleted");
    }

    private static bool Deleted(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
