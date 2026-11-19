using Winshell.Native;
using Winshell.AppWindows;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class VirtualDesktopBackendTests
{
    [TestMethod]
    public void The_contract_names_this_build()
    {
        var build = WindowsVersion.Read().Build;
        Assert.IsTrue(build > 0);
        var contract = VirtualDesktopBackend.Shared.Contract;
        if (contract.Supported)
            Assert.AreEqual("", contract.Reason);
        else
            Assert.Contains($"{build}", contract.Reason, StringComparison.Ordinal);
    }

    [TestMethod]
    public void This_machine_reads_its_desktops_when_the_table_is_known()
    {
        var read = VirtualDesktopBackend.Shared.TryRead(out var desktops, out var current, out var error);
        if (!read)
        {
            Assert.StartsWith("unavailable:", error, StringComparison.Ordinal);
            return;
        }

        Assert.IsNotEmpty(desktops);
        Assert.Contains(desktop => desktop.Id == current, desktops);
        foreach (var desktop in desktops)
            Assert.AreNotEqual(Guid.Empty, desktop.Id);
        var foreground = WindowDesktop.IdOf(PInvoke.GetForegroundWindow());
        if (foreground.Length > 0)
            Assert.IsTrue(Guid.TryParse(foreground, out _));

        var service = new VirtualDesktopsService(new ManualEventLoop(), new Log(), VirtualDesktopBackend.Shared);
        using var stopService = service.Stopping();
        Assert.IsTrue(service.Supported);
        Assert.AreEqual(desktops.Count, service.Desktops.Count);
        foreach (var row in service.Desktops)
            Assert.IsFalse(string.IsNullOrWhiteSpace(row.Name));
    }
}
