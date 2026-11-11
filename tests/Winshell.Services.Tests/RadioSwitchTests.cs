using Windows.Devices.Radios;

namespace Winshell.Services.Tests;

public sealed class RadioSwitchTests
{
    [Theory]
    [InlineData(RadioState.Unknown)]
    [InlineData(RadioState.Unknown, RadioState.Unknown)]
    [InlineData(RadioState.On, RadioState.On)]
    [InlineData(RadioState.Off, RadioState.Off)]
    [InlineData(RadioState.Disabled, RadioState.Disabled)]
    [InlineData(RadioState.On, RadioState.Disabled, RadioState.Off, RadioState.On)]
    [InlineData(RadioState.Off, RadioState.Disabled, RadioState.Off)]
    [InlineData(RadioState.Off, RadioState.Unknown, RadioState.Off)]
    [InlineData(RadioState.Disabled, RadioState.Disabled, RadioState.Unknown)]
    public void A_reading_is_on_when_any_radio_is_and_does_not_guess_when_none_can_say(RadioState expected,
        params RadioState[] states)
    {
        IDeviceRadio[] radios = [.. states.Select(state => new FakeRadio(RadioKind.WiFi, state))];
        Assert.Equal(expected, RadioReading.Read(radios).State);
    }

    [Fact]
    public void A_status_has_one_sentence_and_the_feature_supplies_the_missing_one()
    {
        Assert.Null(RadioSwitch.Describe(RadioSwitchStatus.Done, "missing"));
        Assert.Equal("missing", RadioSwitch.Describe(RadioSwitchStatus.Missing, "missing"));
        Assert.Equal("The user denied radio control.",
            RadioSwitch.Describe(RadioSwitchStatus.DeniedByUser, "missing"));
        Assert.Equal("Windows or its policy denied radio control.",
            RadioSwitch.Describe(RadioSwitchStatus.DeniedBySystem, "missing"));
        Assert.Equal("Windows did not allow radio control.",
            RadioSwitch.Describe(RadioSwitchStatus.NotAllowed, "missing"));
        Assert.Equal(RadioSwitch.Unchanged, RadioSwitch.Describe(RadioSwitchStatus.Unchanged, "missing"));
    }

    [Fact]
    public async Task The_current_value_and_a_missing_radio_do_not_request_access()
    {
        var wifi = new FakeRadio(RadioKind.WiFi, RadioState.On);
        var bluetooth = new FakeRadio(RadioKind.Bluetooth, RadioState.Off);

        Assert.Equal(RadioSwitchStatus.Done,
            await RadioSwitch.SetEnabledAsync(RadioKind.WiFi, [wifi, bluetooth], true));
        Assert.Equal(0, wifi.AccessRequests);
        Assert.Equal(0, bluetooth.AccessRequests);
        Assert.Equal(0, wifi.Sets);

        Assert.Equal(RadioSwitchStatus.Missing,
            await RadioSwitch.SetEnabledAsync(RadioKind.Bluetooth, [wifi], true));
        Assert.Equal(0, wifi.AccessRequests);
    }

    [Fact]
    public async Task An_empty_list_does_not_request_access()
    {
        Assert.Equal(RadioSwitchStatus.Missing, await RadioSwitch.SetEnabledAsync(RadioKind.WiFi, [], true));
    }

    [Fact]
    public async Task Access_refusal_does_not_set_the_radio()
    {
        await Refuse(RadioAccessStatus.DeniedByUser, RadioSwitchStatus.DeniedByUser);
        await Refuse(RadioAccessStatus.DeniedBySystem, RadioSwitchStatus.DeniedBySystem);
        await Refuse(RadioAccessStatus.Unspecified, RadioSwitchStatus.NotAllowed);

        static async Task Refuse(RadioAccessStatus status, RadioSwitchStatus expected)
        {
            var radio = new FakeRadio(RadioKind.WiFi, RadioState.Off) { Access = status };
            Assert.Equal(expected, await RadioSwitch.SetEnabledAsync(RadioKind.WiFi, [radio], true));
            Assert.Equal(1, radio.AccessRequests);
            Assert.Equal(0, radio.Sets);
            Assert.Equal(RadioState.Off, radio.State);
        }
    }

    [Fact]
    public async Task A_refused_set_and_a_state_windows_did_not_reach_are_failures()
    {
        var denied = new FakeRadio(RadioKind.Bluetooth, RadioState.Off) { Result = RadioAccessStatus.DeniedBySystem };
        Assert.Equal(RadioSwitchStatus.DeniedBySystem,
            await RadioSwitch.SetEnabledAsync(RadioKind.Bluetooth, [denied], true));
        Assert.Equal(1, denied.AccessRequests);
        Assert.Equal(1, denied.Sets);
        Assert.Equal(RadioState.Off, denied.State);

        var stuck = new FakeRadio(RadioKind.WiFi, RadioState.On) { Reach = false };
        Assert.Equal(RadioSwitchStatus.Unchanged, await RadioSwitch.SetEnabledAsync(RadioKind.WiFi, [stuck], false));
        Assert.Equal(1, stuck.AccessRequests);
        Assert.Equal(RadioState.On, stuck.State);
    }

    [Fact]
    public async Task Only_radios_of_the_requested_kind_change_and_one_refusal_stops_the_rest()
    {
        var wifiOff = new FakeRadio(RadioKind.WiFi, RadioState.Off);
        var wifiOn = new FakeRadio(RadioKind.WiFi, RadioState.On);
        var bluetooth = new FakeRadio(RadioKind.Bluetooth, RadioState.On);

        Assert.Equal(RadioSwitchStatus.Done,
            await RadioSwitch.SetEnabledAsync(RadioKind.WiFi, [wifiOff, wifiOn, bluetooth], false));
        Assert.Equal(1, wifiOff.AccessRequests);
        Assert.Equal(0, wifiOff.Sets);
        Assert.Equal(0, wifiOn.AccessRequests);
        Assert.Equal(1, wifiOn.Sets);
        Assert.Equal(RadioState.Off, wifiOn.State);
        Assert.Equal(0, bluetooth.AccessRequests);
        Assert.Equal(0, bluetooth.Sets);

        var blocked = new FakeRadio(RadioKind.WiFi, RadioState.Disabled) { Result = RadioAccessStatus.DeniedBySystem };
        var other = new FakeRadio(RadioKind.WiFi, RadioState.Off);
        Assert.Equal(RadioSwitchStatus.DeniedBySystem,
            await RadioSwitch.SetEnabledAsync(RadioKind.WiFi, [blocked, other], true));
        Assert.Equal(1, blocked.AccessRequests);
        Assert.Equal(1, blocked.Sets);
        Assert.Equal(0, other.AccessRequests);
        Assert.Equal(0, other.Sets);
        Assert.Equal(RadioState.Disabled, blocked.State);
        Assert.Equal(RadioState.Off, other.State);
    }

    [Fact]
    public async Task Turning_off_leaves_a_disabled_radio_alone_and_turning_on_reaches_off_radios()
    {
        var on = new FakeRadio(RadioKind.Bluetooth, RadioState.On);
        var disabled = new FakeRadio(RadioKind.Bluetooth, RadioState.Disabled);
        Assert.Equal(RadioSwitchStatus.Done,
            await RadioSwitch.SetEnabledAsync(RadioKind.Bluetooth, [on, disabled], false));
        Assert.Equal(1, on.AccessRequests);
        Assert.Equal(RadioState.Off, on.State);
        Assert.Equal(0, disabled.AccessRequests);
        Assert.Equal(0, disabled.Sets);

        var off = new FakeRadio(RadioKind.WiFi, RadioState.Off);
        Assert.Equal(RadioSwitchStatus.Done, await RadioSwitch.SetEnabledAsync(RadioKind.WiFi, [off], true));
        Assert.Equal(RadioState.On, off.State);
        Assert.Equal(1, off.AccessRequests);
        Assert.Equal(1, off.Sets);
    }
}
