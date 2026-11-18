using Winshell.Tray;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class TrayIconModuleTests
{
    [TestMethod]
    [DataRow(@"C:\Windows\System32\shell32.dll")]
    [DataRow(@"\\?\C:\Windows\System32\shell32.dll")]
    [DataRow("d:/apps/app.exe")]
    [DataRow("shell32.dll")]
    [DataRow("")]
    public void A_local_path_is_accepted(string path)
    {
        Assert.IsTrue(IconImage.IsLocalPath(path));
    }

    [TestMethod]
    [DataRow(@"\\server\share\x.dll")]
    [DataRow(@"\\?\UNC\server\share\x.dll")]
    [DataRow(@"\\.\pipe\x")]
    [DataRow(@"\\?\GLOBALROOT\Device\x.dll")]
    [DataRow(@"\\?\C:")]
    [DataRow("//server/share/x.dll")]
    [DataRow(@"\/server\share\x.dll")]
    public void A_network_or_device_path_is_refused(string path)
    {
        Assert.IsFalse(IconImage.IsLocalPath(path));
    }
}
