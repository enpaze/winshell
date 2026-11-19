using Winshell.Power;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class PowerModeBackendTests
{
    [TestMethod]
    public void Power_mode_guids_round_trip_and_the_deprecated_performance_guid_is_only_read()
    {
        var bestEfficiency = new Guid("961cc777-2547-4f9d-8174-7d86181b8a7a");
        var bestPerformance = new Guid("ded574b5-45a0-4f42-8737-46345c09c238");
        var deprecatedPerformance = new Guid("3af9b8d9-7c97-431d-ad78-34a8bfea439f");
        Assert.AreEqual(PowerModeLevel.BestEfficiency, PowerModeBackend.FromGuid(bestEfficiency));
        Assert.AreEqual(PowerModeLevel.Balanced, PowerModeBackend.FromGuid(Guid.Empty));
        Assert.AreEqual(PowerModeLevel.BestPerformance, PowerModeBackend.FromGuid(bestPerformance));
        Assert.AreEqual(PowerModeLevel.BestPerformance, PowerModeBackend.FromGuid(deprecatedPerformance));
        Assert.AreEqual(bestEfficiency, PowerModeBackend.ToGuid(PowerModeLevel.BestEfficiency));
        Assert.AreEqual(Guid.Empty, PowerModeBackend.ToGuid(PowerModeLevel.Balanced));
        Assert.AreEqual(bestPerformance, PowerModeBackend.ToGuid(PowerModeLevel.BestPerformance));

        Assert.AreEqual(0, (int)EffectivePowerMode.EnergySaver);
        Assert.AreEqual(2, (int)EffectivePowerMode.Balanced);
        Assert.AreEqual(3, (int)PowerModeLevel.Unknown);
        Assert.AreEqual(-1, (int)EffectivePowerMode.Unknown);
        using var monitor = new IdleMonitor(new ManualEventLoop(), new Log(), () => false);
        Assert.IsTrue(monitor.RespectKeepAwake);
    }

    [TestMethod]
    public void An_unanswered_power_mode_reads_as_unknown_and_a_reported_balanced_mode_stays_balanced()
    {
        Assert.AreEqual(PowerModeLevel.Unknown, PowerModeBackend.LevelFrom(127, Guid.Empty));
        Assert.AreEqual(PowerModeLevel.Balanced, PowerModeBackend.LevelFrom(0, Guid.Empty));
        Assert.AreEqual(EffectivePowerMode.Balanced, PowerModeBackend.EffectiveFrom(7));
        Assert.AreEqual(EffectivePowerMode.Unknown, PowerModeBackend.EffectiveFrom((int)EffectivePowerMode.Unknown));
        Assert.AreEqual(EffectivePowerMode.Balanced, PowerModeBackend.EffectiveFrom((int)EffectivePowerMode.Balanced));
    }
}
