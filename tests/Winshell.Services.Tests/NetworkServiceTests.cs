using System.ComponentModel;
using System.Reflection;
using Windows.Devices.Radios;
using Winshell.Network;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class NetworkServiceTests
{
    private const string Secret = "winshell-profile-key-9f3a";

    private const string SavedXml = """
                                    <WLANProfile xmlns="http://www.microsoft.com/networking/WLAN/profile/v1">
                                    	<name>Office</name>
                                    	<SSIDConfig>
                                    		<SSID>
                                    			<name>Cafe</name>
                                    		</SSID>
                                    		<nonBroadcast>true</nonBroadcast>
                                    	</SSIDConfig>
                                    	<connectionMode>auto</connectionMode>
                                    	<MSM>
                                    		<security>
                                    			<authEncryption>
                                    				<authentication>WPA2PSK</authentication>
                                    				<encryption>AES</encryption>
                                    			</authEncryption>
                                    			<sharedKey>
                                    				<keyType>passPhrase</keyType>
                                    				<protected>true</protected>
                                    				<keyMaterial>winshell-profile-key-9f3a</keyMaterial>
                                    			</sharedKey>
                                    		</security>
                                    	</MSM>
                                    </WLANProfile>
                                    """;

    private static readonly Guid AdapterId = Guid.Parse("0b0f2811-87b5-48d6-9d7a-7a26ae12ff33");

    private static NetworkInfo Wifi(string name = "Cafe", double strength = .6, params WifiProfileInfo[] profiles)
    {
        return new NetworkInfo(name, ConnectionState.Disconnected, true, strength, WifiSecurityType.Wpa2Psk, profiles);
    }

    private static NetworkDeviceInfo Adapter(params NetworkInfo[] networks)
    {
        return new NetworkDeviceInfo(AdapterId, DeviceType.Wifi, "Wi-Fi", "00:11:22:33:44:55",
            ConnectionState.Disconnected, false, 0,
            networks, WifiDeviceMode.Station);
    }

    private static NetworkSnapshot Snapshot(params NetworkInfo[] networks)
    {
        return new NetworkSnapshot(true, [Adapter(networks)], NetworkConnectivity.InternetAccess, WifiOn(),
            [
                new NetworkProfileInfo("profile", "Home", AdapterId, NetworkConnectivity.InternetAccess,
                    NetworkCost.Fixed, false, false,
                    .8)
            ],
            NetworkAccessStatus.Allowed, AirplaneModeState.Off);
    }

    private static RadioReading WifiOn()
    {
        return RadioReading.Read([new FakeRadio(RadioKind.WiFi, RadioState.On)]);
    }

    private static NetworkSnapshot Reading(FakeRadio radio)
    {
        return Snapshot() with { Wifi = RadioReading.Read([radio]) };
    }

    private static WifiProfileInfo Described()
    {
        return WifiProfiles.Describe(AdapterId, "Office", SavedXml, 0);
    }

    private static List<string> Watch(INotifyPropertyChanged source)
    {
        var changes = new List<string>();
        source.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);
        return changes;
    }

    private static List<string> Failures(Action<Action<string>> subscribe)
    {
        var messages = new List<string>();
        subscribe(messages.Add);
        return messages;
    }

    [TestMethod]
    public void A_snapshot_changes_only_the_properties_that_differ()
    {
        using var session = new Session(Snapshot(Wifi()));
        var changes = Watch(session.Network);

        session.Backend.Publish(Snapshot(Wifi(strength: .9)));
        Assert.IsEmpty(changes);
        session.Loop.Process();

        Assert.AreSequenceEqual([nameof(WifiNetwork.SignalStrength)], changes);
        Assert.AreEqual(.9, session.Network.SignalStrength);
    }

    [TestMethod]
    public void A_snapshot_keeps_the_objects_that_stay_and_lets_go_of_the_others()
    {
        using var session = new Session(Snapshot(Wifi(), Wifi("Other", .4)));
        var device = session.Device;
        var kept = device.Networks[1];
        var profile = Assert.ContainsSingle(session.Service.Profiles);
        Assert.IsTrue(profile.Metered);
        Assert.AreEqual(AdapterId.ToString(), profile.AdapterId);

        session.Show(Snapshot(Wifi("Other", .9)));

        Assert.AreSame(device, session.Device);
        Assert.AreSame(kept, Assert.ContainsSingle(device.Networks));
        Assert.AreSame(profile, Assert.ContainsSingle(session.Service.Profiles));
        Assert.AreEqual(.9, Assert.IsExactInstanceOfType<WifiNetwork>(kept).SignalStrength);

        session.Show(NetworkSnapshot.Initial);

        Assert.IsEmpty(session.Service.Devices);
        Assert.IsEmpty(session.Service.Profiles);
    }

    [TestMethod]
    public void A_disposed_service_stops_following_the_backend()
    {
        var backend = new FakeBackend();
        var loop = new ManualEventLoop();
        var service = new NetworkService(loop, Log.Default, backend);

        backend.Publish(Snapshot(Wifi()));
        Assert.IsEmpty(service.Devices);
        service.Stop();
        loop.Process();

        Assert.IsEmpty(service.Devices);
        Assert.IsTrue(backend.Disposed);
    }

    [TestMethod]
    public void Reading_never_performs_an_action_or_asks_for_permission()
    {
        using var session = new Session(Snapshot(Wifi(profiles: Described())));

        _ = session.Profile.Id;
        _ = session.Profile.Security;
        _ = session.Profile.HasPassword;

        Assert.IsEmpty(session.Backend.Calls);
    }

    [TestMethod]
    public async Task A_failed_action_ends_busy_then_sets_the_error_then_reports_it()
    {
        using var session = new Session(Snapshot(Wifi()));
        var network = session.Network;
        var device = session.Device;
        var heard = new List<string>();
        network.PropertyChanged += (_, e) => heard.Add("network " + e.PropertyName);
        network.OperationFailed += error => heard.Add("network failed " + error);
        device.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(WifiDevice.Scanning) or nameof(WifiDevice.Error))
                heard.Add("device " + e.PropertyName);
        };
        device.OperationFailed += error => heard.Add("device failed " + error);
        session.Backend.Failure = new UnauthorizedAccessException("Access revoked");

        await network.ConnectAsync(null);
        session.Loop.Process();
        await device.ScanAsync();
        session.Loop.Process();
        session.Backend.Failure = null;
        await network.DisconnectAsync();
        session.Loop.Process();

        Assert.AreSequenceEqual(
        [
            "network Busy", "network Busy", "network Error", "network failed Access revoked",
            "device Scanning", "device Scanning", "device Error", "device failed Access revoked",
            "network Busy", "network Error", "network Busy"
        ], heard);
    }

    [TestMethod]
    public async Task Actions_call_the_backend_and_report_failure()
    {
        using var session = new Session(Snapshot(Wifi()));
        var network = session.Network;
        var failed = Failures(h => network.OperationFailed += h);

        await network.ConnectAsync("secret");
        session.Loop.Process();
        Assert.AreEqual((AdapterId, "Cafe", "secret"), session.Backend.Connection);
        Assert.AreEqual("", network.Error);
        await network.DisconnectAsync();
        session.Loop.Process();
        await network.ForgetAsync();
        session.Loop.Process();
        await session.Device.ScanAsync();
        session.Loop.Process();
        Assert.AreSequenceEqual(["connect", "disconnect", "forget", "scan"], session.Backend.Calls);

        session.Backend.Failure = new UnauthorizedAccessException("Access revoked");
        await network.ConnectAsync(null);
        session.Loop.Process();
        Assert.IsFalse(network.Busy);
        Assert.AreEqual("Access revoked", network.Error);
        Assert.AreSequenceEqual(["Access revoked"], failed);
        await session.Device.ScanAsync();
        session.Loop.Process();
        Assert.IsFalse(session.Device.Scanning);
        Assert.AreEqual("Access revoked", session.Device.Error);

        session.Backend.Failure = null;
        session.Backend.ScanResult = "Call NetworkService.RequestAccess() before scanning Wi-Fi networks.";
        await session.Device.ScanAsync();
        session.Loop.Process();
        Assert.IsFalse(session.Device.Scanning);
        Assert.AreEqual(
            "Call NetworkService.RequestAccess() before scanning Wi-Fi networks.",
            session.Device.Error);

        session.Backend.Failure = null;
        session.Backend.ConnectResult = ConnectionFailReason.InvalidPassword;
        await network.ConnectAsync("wrong");
        session.Loop.Process();
        Assert.AreEqual("InvalidPassword", network.Error);
    }

    [TestMethod]
    public async Task An_action_in_flight_ignores_another_and_a_network_that_left_hears_nothing()
    {
        using var session = new Session(Snapshot(Wifi()));
        var pending = new TaskCompletionSource<ConnectionFailReason?>();
        session.Backend.ConnectTask = pending.Task;
        var network = session.Network;
        var failed = Failures(h => network.OperationFailed += h);

        var first = network.ConnectAsync(null);
        await network.ConnectAsync(null);
        Assert.IsTrue(network.Busy);
        Assert.ContainsSingle(session.Backend.Calls);

        session.Show(Snapshot());
        pending.SetResult(ConnectionFailReason.Timeout);
        await first;
        session.Loop.Process();
        Assert.AreEqual("", network.Error);
        Assert.IsEmpty(failed);

        network.Connect();
        Assert.ContainsSingle(session.Backend.Calls);
    }

    [TestMethod]
    public async Task Access_is_explicit_and_reports_a_failure()
    {
        using var session = new Session(NetworkSnapshot.Initial);
        var completion = new TaskCompletionSource();
        session.Backend.AccessTask = completion.Task;
        var failed = Failures(h => session.Service.OperationFailed += h);

        var request = session.Service.RequestAccessAsync();
        await session.Service.RequestAccessAsync();
        Assert.ContainsSingle(session.Backend.Calls);
        Assert.IsTrue(session.Service.RequestingAccess);
        completion.SetResult();
        await request;
        session.Loop.Process();
        Assert.IsFalse(session.Service.RequestingAccess);

        session.Backend.Failure = new UnauthorizedAccessException("Denied");
        await session.Service.RequestAccessAsync();
        session.Loop.Process();
        Assert.AreEqual("Denied", session.Service.Error);
        Assert.AreSequenceEqual(["Denied"], failed);
    }

    [TestMethod]
    [DataRow(NetworkCost.Unknown, false, false, false)]
    [DataRow(NetworkCost.Unrestricted, false, false, false)]
    [DataRow(NetworkCost.Fixed, false, false, true)]
    [DataRow(NetworkCost.Variable, false, false, true)]
    [DataRow(NetworkCost.Unrestricted, true, false, true)]
    [DataRow(NetworkCost.Unrestricted, false, true, true)]
    public void Metered_uses_cost_roaming_and_data_limit(NetworkCost cost, bool roaming, bool overLimit, bool expected)
    {
        var profile = new NetworkProfile(new NetworkProfileInfo("profile", "Home", AdapterId,
            NetworkConnectivity.LocalAccess, cost, roaming, overLimit, null));

        Assert.AreEqual(expected, profile.Metered);
        Assert.IsFalse(profile.SignalAvailable);
        Assert.AreEqual(0, profile.SignalStrength);
    }

    [TestMethod]
    public void Wired_and_wifi_adapters_report_their_own_state_and_only_a_real_change()
    {
        var wiredId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var wired = new NetworkDeviceInfo(wiredId, DeviceType.Wired, "Ethernet", "aa:bb:cc:dd:ee:ff",
            ConnectionState.Connected, true, 1000, []);

        NetworkSnapshot Shot(NetworkDeviceInfo wifi, NetworkDeviceInfo ethernet)
        {
            return new NetworkSnapshot(true, [wifi, ethernet], NetworkConnectivity.InternetAccess, WifiOn());
        }

        using var session = new Session(Shot(Adapter(), wired));
        var device = Assert.IsExactInstanceOfType<WifiDevice>(session.Service.Devices[0]);
        var ethernet = Assert.IsExactInstanceOfType<WiredDevice>(session.Service.Devices[1]);
        Assert.AreEqual(WifiDeviceMode.Station, device.Mode);
        Assert.AreEqual(1000, ethernet.LinkSpeed);
        Assert.IsTrue(ethernet.HasLink);
        Assert.IsTrue(ethernet.Connected);
        var changes = Watch(device);
        var wiredChanges = Watch(ethernet);

        session.Show(Shot(Adapter() with { Name = "Wi-Fi 2" }, wired));
        session.Show(Shot(Adapter() with { Name = "Wi-Fi 2", Mode = WifiDeviceMode.AccessPoint },
            wired with { HasLink = false, LinkSpeed = 0 }));

        Assert.AreSequenceEqual([nameof(WifiDevice.Name), nameof(WifiDevice.Mode)], changes);
        Assert.AreSequenceEqual([nameof(WiredDevice.LinkSpeed), nameof(WiredDevice.HasLink)], wiredChanges);
    }

    [TestMethod]
    public async Task Scanning_without_an_adapter_reports_the_request_error()
    {
        using var backend = new NetworkBackend(Log.Default);

        Assert.AreEqual("Call NetworkService.RequestAccess() before scanning Wi-Fi networks.",
            await backend.ScanAsync(Guid.NewGuid()));
    }

    [TestMethod]
    [DataRow(RadioState.On, WifiRadioState.On, true, true)]
    [DataRow(RadioState.Off, WifiRadioState.Off, false, true)]
    [DataRow(RadioState.Disabled, WifiRadioState.Disabled, false, false)]
    [DataRow(RadioState.Unknown, WifiRadioState.Unknown, false, false)]
    public void Wifi_reports_its_radios_state(RadioState radio, WifiRadioState expected, bool enabled, bool hardware)
    {
        using var session = new Session(Reading(new FakeRadio(RadioKind.WiFi, radio)));

        Assert.AreEqual(expected, session.Service.WifiState);
        Assert.AreEqual(enabled, session.Service.WifiEnabled);
        Assert.AreEqual(hardware, session.Service.WifiHardwareEnabled);
    }

    [TestMethod]
    public async Task Setting_wifi_without_a_radio_reports_it()
    {
        using var session = new Session(Snapshot() with { Wifi = RadioReading.None });

        await session.Service.SetWifiEnabledAsync(true);
        session.Loop.Process();

        Assert.IsFalse(session.Service.WifiEnabled);
        Assert.AreEqual("Windows reports no Wi-Fi radio.", session.Service.Error);
    }

    [TestMethod]
    public async Task A_switch_sentence_still_shows_when_the_radio_list_failed()
    {
        using var session = new Session(Snapshot() with
        {
            Wifi = RadioReading.None,
            Error = "Windows would not list the radios."
        });
        Assert.AreEqual("Windows would not list the radios.", session.Service.Error);

        await session.Service.SetWifiEnabledAsync(true);
        session.Loop.Process();

        Assert.AreEqual("Windows reports no Wi-Fi radio.", session.Service.Error);
    }

    [TestMethod]
    public async Task Setting_wifi_follows_the_snapshot_and_a_refusal_stays_until_the_radios_change()
    {
        var radio = new FakeRadio(RadioKind.WiFi, RadioState.Off);
        using var session = new Session(Reading(radio));
        var service = session.Service;
        var failed = Failures(h => service.OperationFailed += h);

        await service.SetWifiEnabledAsync(false);
        Assert.AreEqual(0, radio.Sets);

        await service.SetWifiEnabledAsync(true);
        Assert.IsFalse(service.WifiEnabled);
        session.Loop.Process();
        Assert.IsTrue(service.WifiEnabled);
        Assert.AreEqual(RadioState.On, radio.State);
        Assert.AreEqual("", service.Error);
        Assert.IsEmpty(failed);

        radio.Access = RadioAccessStatus.DeniedByUser;
        await service.SetWifiEnabledAsync(false);
        session.Loop.Process();
        Assert.IsTrue(service.WifiEnabled);
        Assert.AreEqual("The user denied radio control.", service.Error);
        Assert.AreEqual(1, radio.Sets);

        radio.Access = RadioAccessStatus.Unspecified;
        await service.SetWifiEnabledAsync(false);
        session.Loop.Process();
        Assert.IsTrue(service.WifiEnabled);
        Assert.AreEqual("Windows did not allow radio control.", service.Error);
        Assert.AreEqual(1, radio.Sets);

        radio.Access = RadioAccessStatus.Allowed;
        radio.Result = RadioAccessStatus.DeniedBySystem;
        await service.SetWifiEnabledAsync(false);
        session.Loop.Process();
        Assert.IsTrue(service.WifiEnabled);
        Assert.AreEqual("Windows or its policy denied radio control.", service.Error);
        Assert.AreEqual(RadioState.On, radio.State);

        radio.Result = RadioAccessStatus.Allowed;
        radio.Reach = false;
        await service.SetWifiEnabledAsync(false);
        session.Loop.Process();
        Assert.AreEqual(RadioSwitch.Unchanged, service.Error);
        Assert.AreEqual(RadioState.On, radio.State);
        session.Show(Reading(radio));
        Assert.AreEqual(RadioSwitch.Unchanged, service.Error);
        Assert.AreSequenceEqual(
            [
                "The user denied radio control.",
                "Windows did not allow radio control.",
                "Windows or its policy denied radio control.",
                RadioSwitch.Unchanged
            ],
            failed);

        radio.Failure = new UnauthorizedAccessException("Access revoked");
        await service.SetWifiEnabledAsync(false);
        session.Loop.Process();
        Assert.AreEqual("Access revoked", service.Error);

        session.Show(Reading(new FakeRadio(RadioKind.WiFi, RadioState.Off)));
        Assert.IsFalse(service.WifiEnabled);
        Assert.AreEqual("", service.Error);
    }

    [TestMethod]
    public async Task A_wifi_request_in_flight_ignores_another_and_a_late_answer_is_dropped()
    {
        var gate = new TaskCompletionSource<RadioAccessStatus>();
        var radio = new FakeRadio(RadioKind.WiFi, RadioState.Off) { Gate = gate };
        using var session = new Session(Reading(radio));

        var first = session.Service.SetWifiEnabledAsync(true);
        await session.Service.SetWifiEnabledAsync(false);
        Assert.AreEqual(1, radio.Sets);

        session.Service.Stop();
        gate.SetResult(RadioAccessStatus.Allowed);
        await first;
        session.Loop.Process();
        Assert.AreEqual("", session.Service.Error);
    }

    [TestMethod]
    public void A_profile_exposes_its_fields_and_no_key()
    {
        using var session = new Session(Snapshot(Wifi(profiles: Described())));
        var profile = session.Profile;

        Assert.AreEqual(WifiSecurityType.Wpa2Psk, profile.Security);
        Assert.IsTrue(profile.HasPassword);
        Assert.IsTrue(profile.Loaded);
        Assert.IsTrue(profile.AutoConnect);
        Assert.AreEqual(AdapterId + ":Office", profile.Id);
        Assert.AreEqual("Office", profile.Name);
        Assert.AreEqual("Cafe", profile.Ssid);
        foreach (var property in typeof(WifiProfile).GetProperties())
        {
            if (property.GetValue(profile) is string text)
                Assert.DoesNotContain(Secret, text);
        }
    }

    [TestMethod]
    public async Task An_unexpected_failure_ends_the_action_and_still_fails_its_task()
    {
        using var session = new Session(Snapshot(Wifi(profiles: Described())));
        var network = session.Network;
        var device = session.Device;
        var profile = session.Profile;
        var bug = new NullReferenceException("A bug");
        session.Backend.Failure = bug;
        session.Backend.AutoException = bug;
        var busy = Watch(network);
        var scanning = Watch(device);
        var requesting = Watch(session.Service);

        Assert.AreSame(bug, await Assert.ThrowsExactlyAsync<NullReferenceException>(() => network.ConnectAsync(null)));
        Assert.AreSame(bug, await Assert.ThrowsExactlyAsync<NullReferenceException>(device.ScanAsync));
        Assert.AreSame(bug, await Assert.ThrowsExactlyAsync<NullReferenceException>(session.Service.RequestAccessAsync));
        Assert.AreSame(bug, await Assert.ThrowsExactlyAsync<NullReferenceException>(() => profile.SetAutoConnectAsync(true)));
        session.Loop.Process();

        Assert.IsFalse(network.Busy);
        Assert.IsFalse(device.Scanning);
        Assert.IsFalse(session.Service.RequestingAccess);
        Assert.AreEqual(2, busy.Count(name => name == "Busy"));
        Assert.AreEqual(2, scanning.Count(name => name == "Scanning"));
        Assert.AreEqual(2, requesting.Count(name => name == "RequestingAccess"));
        session.Backend.AutoException = null;
        await profile.SetAutoConnectAsync(false);
        Assert.AreSequenceEqual([true, false], session.Backend.Autos.Select(a => a.Auto));
    }

    [TestMethod]
    public void A_saved_profile_keeps_its_object_and_a_failed_read_raises_nothing()
    {
        var failedRead = new WifiProfileInfo(WifiFields.Id(AdapterId, "Office"), "Office", "", false,
            WifiSecurityType.Unknown, false, false, false, WifiProfileXml.Unreadable);
        using var session = new Session(Snapshot(Wifi(profiles: Described())));
        var profile = session.Profile;
        var failed = Failures(h => profile.OperationFailed += h);

        session.Show(Snapshot(Wifi(profiles: failedRead)));
        Assert.AreSame(profile, session.Profile);
        Assert.IsFalse(profile.Loaded);
        Assert.AreEqual(WifiProfileXml.Unreadable, profile.Error);
        Assert.IsEmpty(failed);

        var changes = Watch(profile);
        session.Show(Snapshot(Wifi(profiles: failedRead with { AutoConnect = true })));
        Assert.AreSequenceEqual([nameof(WifiProfile.AutoConnect)], changes);

        var second = Described() with { Name = "Office-5", Id = WifiFields.Id(AdapterId, "Office-5") };
        session.Show(Snapshot(Wifi(profiles: [Described(), second])));
        Assert.AreSequenceEqual(["Office", "Office-5"], session.Network.SavedProfiles.Select(p => p.Name));
        Assert.AreSame(profile, session.Network.SavedProfiles[0]);

        session.Show(Snapshot(Wifi()));
        Assert.IsEmpty(session.Network.SavedProfiles);
    }

    [TestMethod]
    public async Task AutoConnect_waits_for_a_snapshot_and_ignores_an_overlapping_write()
    {
        using var session = new Session(Snapshot(Wifi(profiles: Described())));
        var profile = session.Profile;
        var failed = Failures(h => profile.OperationFailed += h);

        await profile.SetAutoConnectAsync(false);
        session.Loop.Process();
        Assert.IsTrue(profile.AutoConnect);
        Assert.AreEqual("", profile.Error);
        Assert.AreEqual((AdapterId, "Office", false), Assert.ContainsSingle(session.Backend.Autos));

        session.Show(Snapshot(Wifi(profiles: Described() with { AutoConnect = false })));
        Assert.AreSame(profile, session.Profile);
        Assert.IsFalse(profile.AutoConnect);

        session.Backend.AutoError = "denied";
        await profile.SetAutoConnectAsync(true);
        session.Loop.Process();
        Assert.AreSequenceEqual(["denied"], failed);
        Assert.AreEqual("denied", profile.Error);
        Assert.IsFalse(profile.AutoConnect);

        session.Backend.AutoError = null;
        session.Backend.AutoException = new UnauthorizedAccessException("Access revoked");
        await profile.SetAutoConnectAsync(true);
        session.Loop.Process();
        Assert.AreEqual("Access revoked", profile.Error);

        session.Backend.AutoException = null;
        var gate = new TaskCompletionSource<string?>();
        session.Backend.AutoGate = gate;
        session.Backend.Autos.Clear();
        var first = profile.SetAutoConnectAsync(true);
        profile.AutoConnect = true;
        Assert.AreEqual((AdapterId, "Office", true), Assert.ContainsSingle(session.Backend.Autos));
        gate.SetResult(null);
        await first;
        session.Loop.Process();
        Assert.IsFalse(profile.AutoConnect);
    }

    [TestMethod]
    public async Task Clear_and_forget_use_the_profile_name()
    {
        using var session = new Session(Snapshot(Wifi(profiles: Described())));
        var profile = session.Profile;
        var failed = Failures(h => profile.OperationFailed += h);

        await profile.ClearPasswordAsync();
        session.Loop.Process();
        Assert.AreEqual((AdapterId, "Office"), Assert.ContainsSingle(session.Backend.Clears));

        session.Backend.ForgetError = "Access is denied.";
        await profile.ForgetAsync();
        session.Loop.Process();
        Assert.AreEqual((AdapterId, "Office"), Assert.ContainsSingle(session.Backend.Forgets));
        Assert.AreSequenceEqual(["Access is denied."], failed);
        Assert.DoesNotContain(Secret, profile.Error);
    }

    [TestMethod]
    public async Task Connecting_with_a_profile_uses_the_network_result_and_refuses_another_networks_profile()
    {
        using var session = new Session(Snapshot(Wifi(profiles: Described())));
        var network = session.Network;
        var profile = session.Profile;
        var failed = Failures(h => network.OperationFailed += h);
        var gate = new TaskCompletionSource<string?>();
        session.Backend.ConnectGate = gate;

        var first = network.ConnectProfileAsync(profile);
        Assert.IsTrue(network.Busy);
        await network.ConnectProfileAsync(profile);
        Assert.AreEqual((AdapterId, "Office", true), Assert.ContainsSingle(session.Backend.Connects));
        gate.SetResult(null);
        await first;
        session.Loop.Process();
        Assert.IsFalse(network.Busy);
        Assert.AreEqual("", network.Error);

        session.Backend.ConnectGate = null;
        session.Backend.ConnectError = "refused";
        await network.ConnectProfileAsync(profile);
        session.Loop.Process();
        Assert.AreSequenceEqual(["refused"], failed);
        Assert.AreEqual("", profile.Error);

        await network.ConnectProfileAsync(null);
        session.Loop.Process();
        Assert.AreEqual("That saved profile is not on this network.", network.Error);

        var other = Described() with { Name = "Home", Ssid = "Home", Id = WifiFields.Id(AdapterId, "Home") };
        session.Backend.Connects.Clear();
        session.Show(Snapshot(Wifi(profiles: Described()), Wifi("Home", profiles: other)));
        var foreign = Assert.ContainsSingle(session.Device.Networks[1].SavedProfiles);
        await network.ConnectProfileAsync(foreign);
        session.Loop.Process();
        Assert.AreEqual("That saved profile is not on this network.", network.Error);

        session.Show(Snapshot(Wifi(profiles: Described())));
        await Assert.IsExactInstanceOfType<WifiNetwork>(session.Device.Networks[0]).ConnectProfileAsync(foreign);
        session.Loop.Process();
        Assert.AreEqual("That saved profile is not on this network.", network.Error);
        Assert.IsEmpty(session.Backend.Connects);
    }

    [TestMethod]
    public void A_wired_network_shares_the_one_list_and_has_no_wifi_command()
    {
        var wiredId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var cable = new NetworkInfo("Office", ConnectionState.Connected, true, 0, WifiSecurityType.Unknown);
        var wired = new NetworkDeviceInfo(wiredId, DeviceType.Wired, "Ethernet", "aa:bb:cc:dd:ee:ff",
            ConnectionState.Connected, true, 1000, [cable]);
        using var session = new Session(new NetworkSnapshot(true, [Adapter(Wifi()), wired],
            NetworkConnectivity.LocalAccess, WifiOn()));

        var networks = session.Service.Devices.SelectMany(device => device.Networks).ToArray();
        Assert.AreEqual(2, networks.Length);
        Assert.DoesNotContain(IsNetworkList, typeof(NetworkService).GetProperties());
        Assert.AreEqual(nameof(NetworkDevice.Networks),
            Assert.ContainsSingle(IsNetworkList, typeof(NetworkDevice).GetProperties()).Name);

        Assert.IsTrue(typeof(Winshell.Network.Network).IsAbstract);
        var wireless = Assert.IsExactInstanceOfType<WifiNetwork>(
            Assert.ContainsSingle(network => network.Device.Type == DeviceType.Wifi, networks));
        var ethernet = Assert.IsExactInstanceOfType<WiredNetwork>(
            Assert.ContainsSingle(network => network.Device.Type == DeviceType.Wired, networks));
        Assert.AreEqual("Office", ethernet.Name);
        Assert.AreEqual(ConnectionState.Connected, ethernet.State);
        Assert.AreEqual(wiredId.ToString(), ethernet.Device.Id);
        Assert.IsEmpty(ethernet.SavedProfiles);
        Assert.IsEmpty(WifiCommands(ethernet.GetType()));
        Assert.IsEmpty(WifiCommands(typeof(Winshell.Network.Network)));
        Assert.AreSequenceEqual(["Connect", "ConnectWithPassword", "ConnectWithProfile", "Disconnect", "Forget"],
            WifiCommands(wireless.GetType()).Order(StringComparer.Ordinal).ToArray());
        Assert.IsEmpty(session.Backend.Calls);
    }

    private static bool IsNetworkList(PropertyInfo property)
    {
        return property.PropertyType == typeof(ObservableList<Winshell.Network.Network>);
    }

    private static IEnumerable<string> WifiCommands(Type type)
    {
        return type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .Where(name => name is "Connect" or "ConnectWithPassword" or "ConnectWithProfile"
                or "Disconnect" or "Forget")
            .Distinct();
    }

    private sealed class Session : IDisposable
    {
        public Session(NetworkSnapshot snapshot)
        {
            Service = new NetworkService(Loop, Log.Default, Backend);
            Show(snapshot);
        }

        public FakeBackend Backend { get; } = new();

        public ManualEventLoop Loop { get; } = new();

        public NetworkService Service { get; }

        public WifiDevice Device => Assert.IsExactInstanceOfType<WifiDevice>(Assert.ContainsSingle(Service.Devices));

        public WifiNetwork Network => Assert.IsExactInstanceOfType<WifiNetwork>(Device.Networks[0]);

        public WifiProfile Profile => Assert.ContainsSingle(Network.SavedProfiles);

        public void Show(NetworkSnapshot snapshot)
        {
            Backend.Publish(snapshot);
            Loop.Process();
        }

        public void Dispose()
        {
            Service.Stop();
        }
    }

    private sealed class FakeBackend : INetworkBackend
    {
        public event Action? Changed;

        private NetworkSnapshot Current { get; set; } = NetworkSnapshot.Initial;

        public List<string> Calls { get; } = [];

        public (Guid, string, string?) Connection { get; private set; }

        public List<(Guid Device, string Name, bool Auto)> Autos { get; } = [];

        public List<(Guid Device, string Name)> Clears { get; } = [];

        public List<(Guid Device, string Name)> Forgets { get; } = [];

        public List<(Guid Device, string Name, bool Hidden)> Connects { get; } = [];

        public Exception? Failure { get; set; }

        public ConnectionFailReason? ConnectResult { get; set; }

        public Task<ConnectionFailReason?>? ConnectTask { get; set; }

        public Task AccessTask { get; set; } = Task.CompletedTask;

        public string? AutoError { get; set; }

        public Exception? AutoException { get; set; }

        public TaskCompletionSource<string?>? AutoGate { get; set; }

        public string? ConnectError { get; set; }

        public TaskCompletionSource<string?>? ConnectGate { get; set; }

        public string? ForgetError { get; set; }

        public bool Disposed { get; private set; }

        public void Publish(NetworkSnapshot snapshot)
        {
            Current = snapshot;
            Changed?.Invoke();
        }

        public Task RequestAccessAsync()
        {
            Calls.Add("access");
            return Failure is { } e ? Task.FromException(e) : AccessTask;
        }

        public Task<NetworkSnapshot> ReadAsync(CancellationToken cancellation)
        {
            Current = Current with { Wifi = RadioReading.Read(Current.Wifi.Radios) };
            return Task.FromResult(Current);
        }

        public string? ScanResult { get; set; }

        public Task<string?> ScanAsync(Guid device)
        {
            Calls.Add("scan");
            Assert.AreEqual(AdapterId, device);
            return Failure is { } e ? Task.FromException<string?>(e) : Task.FromResult(ScanResult);
        }

        public Task<ConnectionFailReason?> ConnectAsync(Guid device, string ssid, string? password)
        {
            Calls.Add("connect");
            Connection = (device, ssid, password);
            return Failure is { } e
                ? Task.FromException<ConnectionFailReason?>(e)
                : ConnectTask ?? Task.FromResult(ConnectResult);
        }

        public Task<string?> DisconnectAsync(Guid device)
        {
            Calls.Add("disconnect");
            Assert.AreEqual(AdapterId, device);
            return Task.FromResult<string?>(null);
        }

        public Task<string?> ForgetAsync(Guid device, string profileName)
        {
            Calls.Add("forget");
            Forgets.Add((device, profileName));
            return Task.FromResult(ForgetError);
        }

        public Task<string?> SetProfileAutoConnectAsync(Guid device, string profileName, bool autoConnect)
        {
            Calls.Add("profile-auto");
            Autos.Add((device, profileName, autoConnect));
            if (AutoException is { } exception)
                throw exception;
            return AutoGate?.Task ?? Task.FromResult(AutoError);
        }

        public Task<string?> ClearProfilePasswordAsync(Guid device, string profileName)
        {
            Calls.Add("profile-clear");
            Clears.Add((device, profileName));
            return Task.FromResult<string?>(null);
        }

        public Task<string?> ConnectProfileAsync(Guid device, string profileName, bool hidden)
        {
            Calls.Add("profile-connect");
            Connects.Add((device, profileName, hidden));
            return ConnectGate?.Task ?? Task.FromResult(ConnectError);
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
