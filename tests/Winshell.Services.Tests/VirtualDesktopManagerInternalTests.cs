using Winshell.Native;
using Winshell.Windows;

namespace Winshell.Services.Tests;

/// <summary>
/// The builds <see cref="VirtualDesktopManagerInternal"/> calls, and the report the virtual desktop service still gives.
/// </summary>
public sealed class VirtualDesktopManagerInternalTests
{
    [Theory]
    [InlineData(22631u, false)]
    [InlineData(26099u, false)]
    [InlineData(26100u, true)]
    [InlineData(26200u, true)]
    [InlineData(26300u, true)]
    [InlineData(26399u, true)]
    [InlineData(26400u, false)]
    [InlineData(28000u, false)]
    public void Only_builds_26100_through_26399_use_the_desktop_method_table(uint build, bool supported)
    {
        Assert.Equal(supported, VirtualDesktopManagerInternal.Known(build));

        var contract = DesktopContract.ForBuild(build);
        Assert.Equal(supported, contract.Supported);
        if (!supported)
            Assert.Contains($"{build}", contract.Reason, StringComparison.Ordinal);
    }
}
