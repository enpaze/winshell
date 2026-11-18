using Winshell.Apps;
using Winshell.Tray;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class ProgramDescriptionTests
{
    [TestMethod]
    public void The_program_description_is_the_file_description()
    {
        var path = typeof(AppsService).Assembly.Location;
        Assert.AreEqual("Winshell.Services", AppsService.ProgramDescription(path));
    }

    [TestMethod]
    public void A_missing_or_unreadable_program_has_no_description()
    {
        using var folder = new TempFolder();
        Assert.IsNull(AppsService.ProgramDescription(Path.Combine(folder.Path, "gone.exe")));
        Assert.IsNull(AppsService.ProgramDescription(""));
    }

    [TestMethod]
    public void A_program_file_with_no_description_has_none()
    {
        using var folder = new TempFolder();
        var path = Path.Combine(folder.Path, "Player.exe");
        File.WriteAllText(path, "not a program");

        Assert.IsNull(AppsService.ProgramDescription(path));
    }

    [TestMethod]
    public void A_tray_icon_shows_that_same_description()
    {
        var path = typeof(AppsService).Assembly.Location;
        const nint hwnd = 0x410;
        var loop = new ManualEventLoop();
        var backend = new FakeTrayBackend();
        backend.Windows[hwnd] = 1;
        backend.Programs[hwnd] = path;
        using var tray = new SystemTray(loop, Log.Default, backend);
        loop.Process();
        backend.Send(new NotifyIconCall
        {
            Message = TrayProtocol.Add,
            Flags = TrayProtocol.NIF_TIP,
            Window = hwnd,
            Id = 1,
            Tip = "Services"
        });

        Assert.AreEqual("Winshell.Services", Assert.ContainsSingle(tray.Items).AppName);
    }
}
