using Winshell.Native;
using Winshell.AppWindows;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class VirtualDesktopBuildsTests
{
    [TestMethod]
    [DataRow(22631u, false)]
    [DataRow(26099u, false)]
    [DataRow(26100u, true)]
    [DataRow(26200u, true)]
    [DataRow(26300u, true)]
    [DataRow(26399u, true)]
    [DataRow(26400u, false)]
    [DataRow(28000u, false)]
    public void Only_builds_26100_through_26399_use_the_desktop_method_table(uint build, bool supported)
    {
        Assert.AreEqual(supported, VirtualDesktopBuilds.Known(build));

        var contract = DesktopContract.ForBuild(build);
        Assert.AreEqual(supported, contract.Supported);
        if (!supported)
            Assert.Contains($"{build}", contract.Reason, StringComparison.Ordinal);
    }
}
