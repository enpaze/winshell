using Windows.Devices.Radios;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class RadioSwitchTests
{
    [TestMethod]
    [DataRow(RadioState.Unknown)]
    [DataRow(RadioState.Unknown, RadioState.Unknown)]
    [DataRow(RadioState.On, RadioState.On)]
    [DataRow(RadioState.Off, RadioState.Off)]
    [DataRow(RadioState.Disabled, RadioState.Disabled)]
    [DataRow(RadioState.On, RadioState.Disabled, RadioState.Off, RadioState.On)]
    [DataRow(RadioState.Off, RadioState.Disabled, RadioState.Off)]
    [DataRow(RadioState.Off, RadioState.Unknown, RadioState.Off)]
    [DataRow(RadioState.Disabled, RadioState.Disabled, RadioState.Unknown)]
    public void A_reading_is_on_when_any_radio_is_and_does_not_guess_when_none_can_say(RadioState expected,
        params RadioState[] states)
    {
        IDeviceRadio[] radios = [.. states.Select(state => new FakeRadio(RadioKind.WiFi, state))];
        Assert.AreEqual(expected, RadioReading.Read(radios).State);
    }

    [TestMethod]
    public void A_status_has_one_sentence_and_the_feature_supplies_the_missing_one()
    {
        Assert.IsNull(RadioSwitch.Describe(RadioSwitchStatus.Done, "missing"));
        Assert.AreEqual("missing", RadioSwitch.Describe(RadioSwitchStatus.Missing, "missing"));
        Assert.AreEqual("The user denied radio control.",
            RadioSwitch.Describe(RadioSwitchStatus.DeniedByUser, "missing"));
        Assert.AreEqual("Windows or its policy denied radio control.",
            RadioSwitch.Describe(RadioSwitchStatus.DeniedBySystem, "missing"));
        Assert.AreEqual("Windows did not allow radio control.",
            RadioSwitch.Describe(RadioSwitchStatus.NotAllowed, "missing"));
        Assert.AreEqual(RadioSwitch.Unchanged, RadioSwitch.Describe(RadioSwitchStatus.Unchanged, "missing"));
    }

    [TestMethod]
    public async Task The_current_value_and_a_missing_radio_do_not_request_access()
    {
        var wifi = new FakeRadio(RadioKind.WiFi, RadioState.On);
        var bluetooth = new FakeRadio(RadioKind.Bluetooth, RadioState.Off);

        Assert.AreEqual(RadioSwitchStatus.Done,
            await RadioSwitch.SetEnabledAsync(RadioKind.WiFi, [wifi, bluetooth], true));
        Assert.AreEqual(0, wifi.AccessRequests);
        Assert.AreEqual(0, bluetooth.AccessRequests);
        Assert.AreEqual(0, wifi.Sets);

        Assert.AreEqual(RadioSwitchStatus.Missing,
            await RadioSwitch.SetEnabledAsync(RadioKind.Bluetooth, [wifi], true));
        Assert.AreEqual(0, wifi.AccessRequests);
    }

    [TestMethod]
    public async Task An_empty_list_does_not_request_access()
    {
        Assert.AreEqual(RadioSwitchStatus.Missing, await RadioSwitch.SetEnabledAsync(RadioKind.WiFi, [], true));
    }

    [TestMethod]
    public async Task Access_refusal_does_not_set_the_radio()
    {
        await Refuse(RadioAccessStatus.DeniedByUser, RadioSwitchStatus.DeniedByUser);
        await Refuse(RadioAccessStatus.DeniedBySystem, RadioSwitchStatus.DeniedBySystem);
        await Refuse(RadioAccessStatus.Unspecified, RadioSwitchStatus.NotAllowed);

        static async Task Refuse(RadioAccessStatus status, RadioSwitchStatus expected)
        {
            var radio = new FakeRadio(RadioKind.WiFi, RadioState.Off) { Access = status };
            Assert.AreEqual(expected, await RadioSwitch.SetEnabledAsync(RadioKind.WiFi, [radio], true));
            Assert.AreEqual(1, radio.AccessRequests);
            Assert.AreEqual(0, radio.Sets);
            Assert.AreEqual(RadioState.Off, radio.State);
        }
    }

    [TestMethod]
    public async Task A_refused_set_and_a_state_windows_did_not_reach_are_failures()
    {
        var denied = new FakeRadio(RadioKind.Bluetooth, RadioState.Off) { Result = RadioAccessStatus.DeniedBySystem };
        Assert.AreEqual(RadioSwitchStatus.DeniedBySystem,
            await RadioSwitch.SetEnabledAsync(RadioKind.Bluetooth, [denied], true));
        Assert.AreEqual(1, denied.AccessRequests);
        Assert.AreEqual(1, denied.Sets);
        Assert.AreEqual(RadioState.Off, denied.State);

        var stuck = new FakeRadio(RadioKind.WiFi, RadioState.On) { Reach = false };
        Assert.AreEqual(RadioSwitchStatus.Unchanged, await RadioSwitch.SetEnabledAsync(RadioKind.WiFi, [stuck], false));
        Assert.AreEqual(1, stuck.AccessRequests);
        Assert.AreEqual(RadioState.On, stuck.State);
    }

    [TestMethod]
    public async Task Only_radios_of_the_requested_kind_change_and_one_refusal_stops_the_rest()
    {
        var wifiOff = new FakeRadio(RadioKind.WiFi, RadioState.Off);
        var wifiOn = new FakeRadio(RadioKind.WiFi, RadioState.On);
        var bluetooth = new FakeRadio(RadioKind.Bluetooth, RadioState.On);

        Assert.AreEqual(RadioSwitchStatus.Done,
            await RadioSwitch.SetEnabledAsync(RadioKind.WiFi, [wifiOff, wifiOn, bluetooth], false));
        Assert.AreEqual(1, wifiOff.AccessRequests);
        Assert.AreEqual(0, wifiOff.Sets);
        Assert.AreEqual(0, wifiOn.AccessRequests);
        Assert.AreEqual(1, wifiOn.Sets);
        Assert.AreEqual(RadioState.Off, wifiOn.State);
        Assert.AreEqual(0, bluetooth.AccessRequests);
        Assert.AreEqual(0, bluetooth.Sets);

        var blocked = new FakeRadio(RadioKind.WiFi, RadioState.Disabled) { Result = RadioAccessStatus.DeniedBySystem };
        var other = new FakeRadio(RadioKind.WiFi, RadioState.Off);
        Assert.AreEqual(RadioSwitchStatus.DeniedBySystem,
            await RadioSwitch.SetEnabledAsync(RadioKind.WiFi, [blocked, other], true));
        Assert.AreEqual(1, blocked.AccessRequests);
        Assert.AreEqual(1, blocked.Sets);
        Assert.AreEqual(0, other.AccessRequests);
        Assert.AreEqual(0, other.Sets);
        Assert.AreEqual(RadioState.Disabled, blocked.State);
        Assert.AreEqual(RadioState.Off, other.State);
    }

    [TestMethod]
    public async Task Turning_off_leaves_a_disabled_radio_alone_and_turning_on_reaches_off_radios()
    {
        var on = new FakeRadio(RadioKind.Bluetooth, RadioState.On);
        var disabled = new FakeRadio(RadioKind.Bluetooth, RadioState.Disabled);
        Assert.AreEqual(RadioSwitchStatus.Done,
            await RadioSwitch.SetEnabledAsync(RadioKind.Bluetooth, [on, disabled], false));
        Assert.AreEqual(1, on.AccessRequests);
        Assert.AreEqual(RadioState.Off, on.State);
        Assert.AreEqual(0, disabled.AccessRequests);
        Assert.AreEqual(0, disabled.Sets);

        var off = new FakeRadio(RadioKind.WiFi, RadioState.Off);
        Assert.AreEqual(RadioSwitchStatus.Done, await RadioSwitch.SetEnabledAsync(RadioKind.WiFi, [off], true));
        Assert.AreEqual(RadioState.On, off.State);
        Assert.AreEqual(1, off.AccessRequests);
        Assert.AreEqual(1, off.Sets);
    }
}
