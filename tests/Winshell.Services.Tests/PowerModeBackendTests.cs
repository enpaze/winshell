using Winshell.Power;

namespace Winshell.Services.Tests;

public sealed class PowerModeBackendTests
{
    [Fact]
    public void Power_mode_guids_round_trip_and_the_deprecated_performance_guid_is_only_read()
    {
        // The overlay GUIDs Windows uses for the power mode slider.
        var bestEfficiency = new Guid("961cc777-2547-4f9d-8174-7d86181b8a7a");
        var bestPerformance = new Guid("ded574b5-45a0-4f42-8737-46345c09c238");
        var deprecatedPerformance = new Guid("3af9b8d9-7c97-431d-ad78-34a8bfea439f");
        Assert.Equal(PowerModeLevel.BestEfficiency, PowerModeBackend.FromGuid(bestEfficiency));
        Assert.Equal(PowerModeLevel.Balanced, PowerModeBackend.FromGuid(Guid.Empty));
        Assert.Equal(PowerModeLevel.BestPerformance, PowerModeBackend.FromGuid(bestPerformance));
        Assert.Equal(PowerModeLevel.BestPerformance, PowerModeBackend.FromGuid(deprecatedPerformance));
        Assert.Equal(bestEfficiency, PowerModeBackend.ToGuid(PowerModeLevel.BestEfficiency));
        Assert.Equal(Guid.Empty, PowerModeBackend.ToGuid(PowerModeLevel.Balanced));
        Assert.Equal(bestPerformance, PowerModeBackend.ToGuid(PowerModeLevel.BestPerformance));

        Assert.Equal(0, (int)EffectivePowerMode.EnergySaver);
        Assert.Equal(2, (int)EffectivePowerMode.Balanced);
        Assert.Equal(3, (int)PowerModeLevel.Unknown);
        Assert.Equal(-1, (int)EffectivePowerMode.Unknown);
        using var monitor = new IdleMonitor(new ManualEventLoop(), new Log(), () => false);
        Assert.True(monitor.RespectKeepAwake);
    }

    [Fact]
    public void An_unanswered_power_mode_reads_as_unknown_and_a_reported_balanced_mode_stays_balanced()
    {
        // 127 is ERROR_PROC_NOT_FOUND, the error a Windows without the power mode returns.
        Assert.Equal(PowerModeLevel.Unknown, PowerModeBackend.LevelFrom(127, Guid.Empty));
        Assert.Equal(PowerModeLevel.Balanced, PowerModeBackend.LevelFrom(0, Guid.Empty));
        // 7 is the V2 energy-saver value this enum does not name. It stays Balanced.
        Assert.Equal(EffectivePowerMode.Balanced, PowerModeBackend.EffectiveFrom(7));
        Assert.Equal(EffectivePowerMode.Unknown, PowerModeBackend.EffectiveFrom((int)EffectivePowerMode.Unknown));
        Assert.Equal(EffectivePowerMode.Balanced, PowerModeBackend.EffectiveFrom((int)EffectivePowerMode.Balanced));
    }
}
