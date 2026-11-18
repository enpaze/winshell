using Winshell.Native;
using Winshell.Tray;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class TrayClickerTests
{
    [TestMethod]
    public void An_elevated_shell_clicks_no_app_above_medium_integrity()
    {
        const uint low = 0x1000, medium = ProcessIntegrity.Medium, high = 0x3000, system = 0x4000;
        Assert.IsFalse(TrayClicker.MayClick(high, high));
        Assert.IsFalse(TrayClicker.MayClick(high, system));
        Assert.IsFalse(TrayClicker.MayClick(high, null));
        Assert.IsTrue(TrayClicker.MayClick(high, medium));
        Assert.IsTrue(TrayClicker.MayClick(high, low));
        Assert.IsFalse(TrayClicker.MayClick(null, high));
        Assert.IsTrue(TrayClicker.MayClick(null, medium));
        Assert.IsTrue(TrayClicker.MayClick(medium, high));
        Assert.IsTrue(TrayClicker.MayClick(medium, medium));
    }

    [TestMethod]
    [DataRow(ProcessIntegrity.Medium, true)]
    [DataRow(0x3000u, false)]
    public void A_click_hands_over_the_foreground_then_reaches_the_app_only_when_this_process_may_click_it(uint shell,
        bool clicks)
    {
        const nint app = 0x100;
        var backend = new FakeTrayBackend
        { Windows = { [app] = 42 }, Integrities = { [42] = 0x3000, [(uint)Environment.ProcessId] = shell } };
        using var clicker = new TrayClicker(new ManualEventLoop(), backend);

        clicker.Click(new TrayIcon { Key = "app", Window = app, Id = 1, CallbackMessage = 0x8001 }, TrayClick.Left,
            null);

        string[] calls = clicks ? ["foreground 100 42", "notify", "notify"] : [];
        (nint, uint, nint, nint)[] sent = clicks ? [(app, 0x8001, 1, 0x201), (app, 0x8001, 1, 0x202)] : [];
        Assert.AreSequenceEqual(calls, backend.Calls);
        Assert.AreSequenceEqual(sent, backend.Notified);
    }
}
