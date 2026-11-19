using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;
using Windows.Devices.Radios;
using Winshell.Bluetooth;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class BluetoothServiceTests
{
    private static readonly Guid Container = Guid.Parse("71886561-73c1-4d67-a4a0-5077c90859d0");

    private static BluetoothAdapterInfo Adapter(string id = "WindowsAdapter", FakeRadio? radio = null)
    {
        return new BluetoothAdapterInfo(id, "Bluetooth", "00:11:22:33:44:55",
            RadioReading.Read([radio ?? new FakeRadio(RadioKind.Bluetooth, RadioState.On)]));
    }

    private static BluetoothDeviceInfo DeviceInfo(string id = "WindowsEndpoint", bool paired = true,
        bool present = true,
        double? battery = .7)
    {
        return new BluetoothDeviceInfo("stable-key", id, "00:11:22:33:44:66", "Headphones", true, paired, present,
            !paired, battery,
            Container, paired ? [id] : []);
    }

    private static BluetoothSnapshot Snapshot(params BluetoothDeviceInfo[] devices)
    {
        return new BluetoothSnapshot(true, Adapter(), devices);
    }

    private static BluetoothSnapshot Visibility(bool discoverable, bool pairable)
    {
        return Snapshot() with { Adapter = Adapter() with { Discoverable = discoverable, Pairable = pairable } };
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

    private static BluetoothService Monitored(IBluetoothBackend backend, ManualEventLoop loop, Log? log = null)
    {
        var service = new BluetoothService(loop, log ?? new Log(), backend);
        service.StartMonitoring();
        return service;
    }

    [TestMethod]
    public async Task A_read_changes_only_the_properties_that_differ()
    {
        using var session = await Session.OpenAsync(Snapshot(DeviceInfo()));
        var changes = Watch(session.Device);

        await session.ShowAsync(Snapshot(DeviceInfo() with { Icon = "audio-headset" }));

        Assert.AreSequenceEqual([nameof(BluetoothDevice.Icon)], changes);
        Assert.AreEqual("audio-headset", session.Device.Icon);
    }

    [TestMethod]
    public async Task A_device_that_stays_is_the_same_object_and_moves_between_the_lists()
    {
        using var session = await Session.OpenAsync(Snapshot(DeviceInfo()));
        var service = session.Service;
        var adapter = service.DefaultAdapter;
        var device = session.Device;
        Assert.AreSame(device, Assert.ContainsSingle(service.PairedDevices));
        Assert.IsEmpty(service.NearbyDevices);

        await session.ShowAsync(Snapshot(DeviceInfo("ChangedEndpoint", false, battery: null) with
        {
            Name = "Nearby",
            Connected = false
        }));

        Assert.AreSame(device, session.Device);
        Assert.AreSame(adapter, service.DefaultAdapter);
        Assert.AreSame(device.Adapter, service.DefaultAdapter);
        Assert.AreEqual("ChangedEndpoint", device.Id);
        Assert.AreEqual("Nearby", device.Name);
        Assert.IsFalse(device.Paired);
        Assert.IsTrue(device.CanPair);
        Assert.AreEqual(BluetoothDeviceState.Disconnected, device.State);
        Assert.IsFalse(device.BatteryAvailable);
        Assert.AreEqual(0, device.Battery);
        Assert.IsEmpty(service.PairedDevices);
        Assert.AreSame(device, Assert.ContainsSingle(service.NearbyDevices));

        await session.ShowAsync(Snapshot());
        Assert.IsEmpty(service.Devices);
        Assert.IsEmpty(service.NearbyDevices);
    }

    [TestMethod]
    public async Task A_new_adapter_replaces_every_object_and_no_adapter_clears_every_list()
    {
        using var session = await Session.OpenAsync(Snapshot(DeviceInfo()));
        var service = session.Service;
        var adapter = service.DefaultAdapter;
        var device = session.Device;

        await session.ShowAsync(Snapshot(DeviceInfo()) with { Adapter = Adapter("Replacement") });

        Assert.AreNotSame(adapter, service.DefaultAdapter);
        Assert.AreNotSame(device, session.Device);
        Assert.AreSame(service.DefaultAdapter, session.Device.Adapter);
        Assert.AreSame(service.DefaultAdapter, Assert.ContainsSingle(service.Adapters));

        await session.ShowAsync(new BluetoothSnapshot(true, null, [DeviceInfo()]));

        Assert.IsNull(service.DefaultAdapter);
        Assert.IsEmpty(service.Adapters);
        Assert.IsEmpty(service.Devices);
        Assert.IsEmpty(service.PairedDevices);
        Assert.IsEmpty(service.NearbyDevices);
    }

    [TestMethod]
    public async Task An_unchanged_read_leaves_the_lists_alone()
    {
        using var session = await Session.OpenAsync(Snapshot(DeviceInfo()));
        var updates = 0;

        void Count(object? sender, NotifyCollectionChangedEventArgs e)
        {
            updates++;
        }

        session.Service.Devices.CollectionChanged += Count;
        session.Service.Adapters.CollectionChanged += Count;
        session.Service.PairedDevices.CollectionChanged += Count;
        session.Service.NearbyDevices.CollectionChanged += Count;

        await session.ShowAsync(Snapshot(DeviceInfo()) with { Discovering = true });
        await session.ShowAsync(Snapshot(DeviceInfo()) with { Discovering = true });

        Assert.AreEqual(0, updates);
    }

    [TestMethod]
    public async Task Actions_use_the_backend_and_never_change_state_ahead_of_a_read()
    {
        using var session = await Session.OpenAsync(Snapshot(DeviceInfo()));
        var backend = session.Backend;
        var device = session.Device;
        Assert.IsEmpty(backend.Calls);

        await device.SetConnectionAsync(false);
        session.Loop.Process();
        Assert.IsTrue(device.Connected);
        Assert.AreEqual((Container, false), Assert.ContainsSingle(backend.Connections));
        await device.SetConnectionAsync(true);
        session.Loop.Process();
        Assert.AreEqual((Container, true), backend.Connections[1]);
        Assert.AreEqual("", device.Error);

        await session.ShowAsync(Snapshot(DeviceInfo(paired: false)));
        await device.PairAsync();
        session.Loop.Process();
        Assert.IsFalse(device.Paired);
        Assert.AreEqual("WindowsEndpoint", Assert.ContainsSingle(backend.Paired));

        await session.ShowAsync(Snapshot(DeviceInfo() with { PairedIds = ["classic", "le"] }));
        await device.UnpairAsync();
        session.Loop.Process();
        Assert.AreSequenceEqual(["classic", "le"], backend.Unpaired);
    }

    [TestMethod]
    public async Task An_unexpected_failure_ends_the_action_and_still_fails_its_task()
    {
        using var session = await Session.OpenAsync(Snapshot(DeviceInfo()));
        var device = session.Device;
        var bug = new NullReferenceException("A bug");
        session.Backend.Failure = bug;

        Assert.AreSame(bug, await Assert.ThrowsExactlyAsync<NullReferenceException>(device.UnpairAsync));
        session.Loop.Process();

        Assert.IsFalse(device.Busy);
    }

    [TestMethod]
    public async Task A_failed_action_sets_the_error_and_raises_the_event()
    {
        using var session = await Session.OpenAsync(Snapshot(DeviceInfo()));
        var backend = session.Backend;
        var device = session.Device;
        var failed = Failures(h => device.OperationFailed += h);

        backend.AudioAccepted = false;
        await device.SetConnectionAsync(true);
        session.Loop.Process();
        Assert.Contains("Other device types", device.Error, StringComparison.Ordinal);
        Assert.AreSequenceEqual([device.Error], failed);

        backend.Failure = new UnauthorizedAccessException("Access denied");
        await device.UnpairAsync();
        session.Loop.Process();
        Assert.AreEqual("Access denied", device.Error);
        Assert.IsFalse(device.Busy);

        await session.ShowAsync(Snapshot(DeviceInfo(paired: false) with { Container = null }));
        var calls = backend.Connections.Count;
        await device.SetConnectionAsync(true);
        session.Loop.Process();
        Assert.AreEqual(calls, backend.Connections.Count);
        Assert.Contains("paired Bluetooth audio device", device.Error, StringComparison.Ordinal);

        backend.Failure = null;
        backend.PairError = "RejectedByHandler";
        await device.PairAsync();
        session.Loop.Process();
        Assert.AreEqual("RejectedByHandler", device.Error);
    }

    [TestMethod]
    public async Task A_pairing_ignores_another_action_and_ends_when_cancelled_or_when_the_service_goes()
    {
        var session = await Session.OpenAsync(Snapshot(DeviceInfo(paired: false)));
        session.Backend.WaitForPairCancellation = true;
        var device = session.Device;

        var request = device.PairAsync();
        await device.PairAsync();
        Assert.IsTrue(device.Pairing);
        Assert.IsTrue(device.Busy);
        Assert.ContainsSingle(session.Backend.Paired);

        device.CancelPairing();
        await request;
        session.Loop.Process();
        Assert.AreEqual("Cancelled", device.Error);
        Assert.IsFalse(device.Pairing);

        request = device.PairAsync();
        session.Dispose();
        await request.WaitAsync(CurrentTest.Cancellation);
        session.Loop.Process();
        Assert.IsTrue(session.Backend.Disposed);
    }

    [TestMethod]
    public async Task A_device_that_leaves_stops_its_pairing()
    {
        using var session = await Session.OpenAsync(Snapshot(DeviceInfo(paired: false)));
        session.Backend.WaitForPairCancellation = true;
        var device = session.Device;
        var failed = Failures(h => device.OperationFailed += h);

        var request = device.PairAsync();
        await session.ShowAsync(Snapshot());
        await request.WaitAsync(CurrentTest.Cancellation);
        session.Loop.Process();

        Assert.IsEmpty(failed);
        Assert.ContainsSingle(session.Backend.Paired);
    }

    [TestMethod]
    public async Task Discovery_is_explicit_and_needs_an_adapter_that_is_on()
    {
        using var session = await Session.OpenAsync(Snapshot());
        var service = session.Service;
        var failed = Failures(h => service.OperationFailed += h);

        service.StartDiscovery();
        session.Loop.Process();
        Assert.IsTrue(service.Discovering);
        service.StopDiscovery();
        session.Loop.Process();
        Assert.IsFalse(service.Discovering);
        Assert.AreSequenceEqual(["start", "stop"], session.Backend.Calls);

        await session.ShowAsync(Snapshot() with
        {
            Adapter = Adapter(radio: new FakeRadio(RadioKind.Bluetooth, RadioState.Off))
        });
        service.StartDiscovery();
        Assert.AreEqual(2, session.Backend.Calls.Count);
        Assert.Contains("must be on", service.Error, StringComparison.Ordinal);
        Assert.AreSequenceEqual([service.Error], failed);
    }

    [TestMethod]
    public async Task A_read_reaches_the_loop_recovers_from_failure_and_a_stale_one_is_dropped()
    {
        using var session = await Session.OpenAsync(Snapshot(DeviceInfo()));
        var service = session.Service;
        var backend = session.Backend;

        backend.ReadFailure = new UnauthorizedAccessException("Unavailable");
        await service.RefreshAsync();
        Assert.IsTrue(service.Ready);
        session.Loop.Process();
        Assert.IsFalse(service.Ready);
        Assert.AreEqual("Unavailable", service.Error);
        Assert.IsEmpty(service.Devices);

        backend.ReadFailure = null;
        await session.ShowAsync(Snapshot(DeviceInfo()));
        Assert.AreEqual("", service.Error);

        var completion = new TaskCompletionSource<BluetoothSnapshot>();
        backend.ReadTask = completion.Task;
        var read = service.RefreshAsync();
        service.Invalidate();
        completion.SetResult(Snapshot(DeviceInfo() with { Name = "Stale" }));
        await read;
        session.Loop.Process();
        Assert.AreEqual("Headphones", session.Device.Name);
    }

    [TestMethod]
    public void A_bluetooth_address_is_six_bytes_high_byte_first()
    {
        Assert.AreEqual("AA:BB:CC:DD:EE:FF", BluetoothBackend.FormatAddress(0xAABBCCDDEEFF));
    }

    [TestMethod]
    public async Task Setting_the_radio_follows_a_read_and_reports_a_refusal()
    {
        var radio = new FakeRadio(RadioKind.Bluetooth, RadioState.On);
        using var session =
            await Session.OpenAsync(new BluetoothSnapshot(true, Adapter(radio: radio), [DeviceInfo()]));
        var service = session.Service;
        var adapter = service.DefaultAdapter!;
        var failed = Failures(h => adapter.OperationFailed += h);

        await service.SetAdapterEnabledAsync(adapter, true);
        Assert.AreEqual(0, radio.Sets);

        await service.SetAdapterEnabledAsync(adapter, false);
        Assert.IsTrue(adapter.Enabled);
        session.Loop.Process();
        Assert.IsFalse(adapter.Enabled);
        Assert.AreEqual(BluetoothAdapterState.Off, adapter.State);
        Assert.AreEqual(RadioState.Off, radio.State);
        Assert.IsEmpty(failed);

        radio.Access = RadioAccessStatus.DeniedByUser;
        await service.SetAdapterEnabledAsync(adapter, true);
        session.Loop.Process();
        Assert.IsFalse(adapter.Enabled);
        Assert.AreEqual("The user denied radio control.", adapter.Error);
        Assert.AreEqual(1, radio.Sets);

        radio.Access = RadioAccessStatus.Unspecified;
        await service.SetAdapterEnabledAsync(adapter, true);
        session.Loop.Process();
        Assert.IsFalse(adapter.Enabled);
        Assert.AreEqual("Windows did not allow radio control.", adapter.Error);
        Assert.AreEqual(1, radio.Sets);

        radio.Access = RadioAccessStatus.Allowed;
        radio.Result = RadioAccessStatus.DeniedBySystem;
        await service.SetAdapterEnabledAsync(adapter, true);
        session.Loop.Process();
        Assert.IsFalse(adapter.Enabled);
        Assert.AreEqual("Windows or its policy denied radio control.", adapter.Error);
        Assert.AreEqual(RadioState.Off, radio.State);

        radio.Result = RadioAccessStatus.Allowed;
        radio.Reach = false;
        await service.SetAdapterEnabledAsync(adapter, true);
        session.Loop.Process();
        Assert.AreEqual(RadioSwitch.Unchanged, adapter.Error);
        Assert.AreEqual(RadioState.Off, radio.State);
        Assert.AreSequenceEqual(
            [
                "The user denied radio control.",
                "Windows did not allow radio control.",
                "Windows or its policy denied radio control.",
                RadioSwitch.Unchanged
            ],
            failed);

        radio.Failure = new UnauthorizedAccessException("Access revoked");
        await service.SetAdapterEnabledAsync(adapter, true);
        session.Loop.Process();
        Assert.IsFalse(adapter.Enabled);
        Assert.AreEqual("Access revoked", adapter.Error);

        await session.ShowAsync(Snapshot(DeviceInfo()));
        Assert.IsTrue(adapter.Enabled);
        Assert.AreEqual("", adapter.Error);
    }

    [TestMethod]
    public async Task A_bug_in_a_radio_write_faults_and_does_not_report_success()
    {
        var bug = new InvalidOperationException("A bug");
        var radio = new FakeRadio(RadioKind.Bluetooth, RadioState.Off)
        {
            Result = RadioAccessStatus.DeniedBySystem
        };
        using var session = await Session.OpenAsync(new BluetoothSnapshot(true, Adapter(radio: radio), []));
        var service = session.Service;
        var adapter = service.DefaultAdapter!;
        var failed = Failures(h => adapter.OperationFailed += h);
        const string refusal = "Windows or its policy denied radio control.";

        await service.SetAdapterEnabledAsync(adapter, true);
        session.Loop.Process();
        Assert.AreEqual(refusal, adapter.Error);

        radio.Failure = bug;
        Assert.AreSame(bug,
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.SetAdapterEnabledAsync(adapter, true)));
        session.Loop.Process();
        Assert.AreSequenceEqual([refusal], failed);
        Assert.AreEqual(refusal, adapter.Error);

        radio.Failure = null;
        await service.SetAdapterEnabledAsync(adapter, true);
        session.Loop.Process();
        Assert.AreEqual(refusal, adapter.Error);
        Assert.AreSequenceEqual([refusal, refusal], failed);
    }

    [TestMethod]
    public async Task An_adapter_without_a_radio_reports_it()
    {
        using var session = await Session.OpenAsync(
            Snapshot() with { Adapter = Adapter() with { Radio = RadioReading.None } });
        var adapter = session.Service.DefaultAdapter!;

        await session.Service.SetAdapterEnabledAsync(adapter, true);
        session.Loop.Process();

        Assert.AreEqual(BluetoothAdapterState.Unknown, adapter.State);
        Assert.AreEqual("Windows reports no Bluetooth radio for this adapter.", adapter.Error);
    }

    [TestMethod]
    public async Task A_radio_request_in_flight_ignores_another_and_a_replaced_adapter_hears_nothing()
    {
        var gate = new TaskCompletionSource<RadioAccessStatus>();
        var radio = new FakeRadio(RadioKind.Bluetooth, RadioState.Off) { Gate = gate };
        using var session = await Session.OpenAsync(new BluetoothSnapshot(true, Adapter(radio: radio), []));
        var service = session.Service;
        var adapter = service.DefaultAdapter!;

        var first = service.SetAdapterEnabledAsync(adapter, true);
        await service.SetAdapterEnabledAsync(adapter, true);
        Assert.AreEqual(1, radio.Sets);

        await session.ShowAsync(new BluetoothSnapshot(true,
            Adapter("Replacement", new FakeRadio(RadioKind.Bluetooth, RadioState.Off)), []));
        gate.SetResult(RadioAccessStatus.DeniedBySystem);
        await first;
        session.Loop.Process();
        Assert.AreEqual("", adapter.Error);
        Assert.AreEqual("", service.DefaultAdapter!.Error);
        Assert.AreEqual(RadioState.Off, radio.State);
    }

    [TestMethod]
    public async Task Discoverable_and_pairable_follow_a_read_and_the_order_Windows_needs()
    {
        using var session = await Session.OpenAsync(Visibility(false, true));
        var service = session.Service;
        var backend = session.Backend;
        var adapter = service.DefaultAdapter!;

        await service.SetDiscoverableAsync(adapter, false);
        Assert.IsEmpty(backend.Calls);

        await service.SetDiscoverableAsync(adapter, true);
        Assert.IsFalse(adapter.Discoverable);
        Assert.AreEqual("discoverable-on", Assert.ContainsSingle(backend.Calls));
        session.Loop.Process();
        Assert.IsTrue(adapter.Discoverable);
        Assert.IsTrue(adapter.Pairable);

        await session.ShowAsync(Visibility(false, false));
        backend.Calls.Clear();
        await service.SetDiscoverableAsync(adapter, true);
        Assert.AreSequenceEqual(["pairable-on", "discoverable-on"], backend.Calls);
        session.Loop.Process();
        Assert.IsTrue(adapter.Discoverable);
        Assert.IsTrue(adapter.Pairable);

        backend.Calls.Clear();
        await service.SetPairableAsync(adapter, false);
        Assert.AreSequenceEqual(["discoverable-off", "pairable-off"], backend.Calls);
        Assert.IsTrue(adapter.Discoverable);
        session.Loop.Process();
        Assert.IsFalse(adapter.Discoverable);
        Assert.IsFalse(adapter.Pairable);
    }

    [TestMethod]
    public async Task A_visibility_failure_is_reported_and_does_not_skip_ahead_of_windows()
    {
        using var session = await Session.OpenAsync(Visibility(false, false));
        var service = session.Service;
        var backend = session.Backend;
        var adapter = service.DefaultAdapter!;
        var failed = Failures(h => adapter.OperationFailed += h);

        backend.PairableError = "incoming refused";
        await service.SetDiscoverableAsync(adapter, true);
        session.Loop.Process();
        Assert.AreEqual("pairable-on", Assert.ContainsSingle(backend.Calls));
        Assert.IsFalse(adapter.Pairable);
        Assert.AreEqual("incoming refused", adapter.Error);
        Assert.AreSequenceEqual(["incoming refused"], failed);

        backend.Calls.Clear();
        backend.PairableError = null;
        backend.DiscoverableError = "discovery refused";
        await service.SetDiscoverableAsync(adapter, true);
        session.Loop.Process();
        Assert.AreSequenceEqual(["pairable-on", "discoverable-on"], backend.Calls);
        Assert.IsTrue(adapter.Pairable);
        Assert.IsFalse(adapter.Discoverable);
        Assert.AreEqual("discovery refused", adapter.Error);

        await session.ShowAsync(Visibility(true, true));
        Assert.AreEqual("", adapter.Error);
    }

    [TestMethod]
    public async Task A_visibility_request_in_flight_ignores_another()
    {
        using var session = await Session.OpenAsync(Visibility(false, true));
        var service = session.Service;
        var adapter = service.DefaultAdapter!;
        var gate = new TaskCompletionSource<string?>();
        session.Backend.VisibilityGate = gate;

        var first = service.SetDiscoverableAsync(adapter, true);
        await service.SetPairableAsync(adapter, false);
        Assert.AreEqual("discoverable-on", Assert.ContainsSingle(session.Backend.Calls));

        gate.SetResult(null);
        await first;
        session.Loop.Process();
        Assert.IsTrue(adapter.Discoverable);
    }

    [TestMethod]
    public void A_new_service_asks_Windows_for_nothing_until_monitoring_starts()
    {
        var backend = new FakeBackend { Current = Snapshot(DeviceInfo()) };

        var service = new BluetoothService(new ManualEventLoop(), new Log(), backend);
        using var stopService = service.Stopping();

        Assert.AreEqual(0, backend.Reads);
        Assert.IsFalse(backend.Watching);
    }

    [TestMethod]
    public void Monitoring_reads_again_when_Windows_reports_a_change_and_ends_with_the_service()
    {
        var backend = new FakeBackend { Current = Snapshot(DeviceInfo()) };
        var loop = new ManualEventLoop();
        var service = Monitored(backend, loop);
        loop.Process();
        Assert.IsTrue(backend.Watching);
        Assert.AreEqual("Headphones", Assert.ContainsSingle(service.Devices).Name);

        backend.Current = Snapshot(DeviceInfo() with { Name = "Renamed" });
        backend.RaiseChanged();
        loop.Process();
        Assert.AreEqual("Renamed", Assert.ContainsSingle(service.Devices).Name);

        service.Stop();
        service.Stop();
        Assert.IsFalse(backend.Watching);
        Assert.IsTrue(backend.Disposed);
        backend.RaiseChanged();
        Assert.AreEqual(2, backend.Reads);
    }

    [TestMethod]
    public void A_slow_read_covers_what_Windows_does_not_announce_and_stops_with_monitoring()
    {
        var backend = new FakeBackend { Current = Snapshot(DeviceInfo()) };
        var loop = new ManualEventLoop();
        var service = Monitored(backend, loop);
        using var stopService = service.Stopping();
        Assert.AreEqual(1, backend.Reads);

        loop.Advance(TimeSpan.FromSeconds(29));
        Assert.AreEqual(1, backend.Reads);
        loop.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(2, backend.Reads);

        service.StopMonitoring();
        Assert.IsFalse(backend.Watching);
        loop.Advance(TimeSpan.FromMinutes(5));
        Assert.AreEqual(2, backend.Reads);

        service.StartMonitoring();
        Assert.IsTrue(backend.Watching);
        Assert.AreEqual(3, backend.Reads);
        loop.Advance(TimeSpan.FromSeconds(30));
        Assert.AreEqual(4, backend.Reads);
    }

    [TestMethod]
    public void A_throwing_read_is_logged_and_does_not_stop_monitoring()
    {
        var backend = new FlakyBackend();
        var loop = new ManualEventLoop();
        var messages = new ConcurrentQueue<string>();
        var log = new Log { Sink = (_, category, message) => messages.Enqueue($"{category}: {message}") };
        var service = Monitored(backend, loop, log);
        using var stopService = service.Stopping();

        Assert.Contains("bluetooth: InvalidCastException: radio broke", messages);
        Assert.IsFalse(service.Ready);
        backend.RaiseChanged();
        loop.Process();
        Assert.IsTrue(service.Ready);
    }

    [TestMethod]
    public async Task A_bug_in_a_read_is_warned_and_the_next_read_still_applies()
    {
        var backend = new FakeBackend { ReadFailure = new InvalidOperationException("radio broke") };
        var loop = new ManualEventLoop();
        var messages = new ConcurrentQueue<string>();
        var log = new Log { Sink = (_, category, message) => messages.Enqueue($"{category}: {message}") };
        var service = new BluetoothService(loop, log, backend);
        using var stopService = service.Stopping();

        await service.RefreshAsync();
        loop.Process();

        Assert.AreEqual("", service.Error);
        Assert.Contains("bluetooth: InvalidOperationException: radio broke", messages);
        backend.ReadFailure = null;
        backend.Current = Snapshot(DeviceInfo());
        await service.RefreshAsync();
        loop.Process();
        Assert.AreEqual("Headphones", Assert.ContainsSingle(service.Devices).Name);
    }

    [TestMethod]
    public async Task A_refresh_still_reads_after_monitoring_stops()
    {
        var backend = new TokenBackend();
        var service = Monitored(backend, new ManualEventLoop());
        using var stopService = service.Stopping();
        service.StopMonitoring();
        Assert.AreEqual(1, backend.Finished);

        await service.RefreshAsync();

        Assert.AreEqual(2, backend.Finished);
    }

    [TestMethod]
    public async Task A_refresh_during_a_read_reads_again_when_it_ends()
    {
        var backend = new GatedBackend();
        var service = new BluetoothService(new ManualEventLoop(), new Log(), backend);
        using var stopService = service.Stopping();
        var first = service.RefreshAsync();
        await backend.Started.Task.WaitAsync(CurrentTest.Cancellation);

        var second = service.RefreshAsync();
        var third = service.RefreshAsync();
        Assert.AreEqual(1, backend.Reads);
        Assert.IsFalse(second.IsCompleted);
        backend.Gate.SetResult();
        await first.WaitAsync(CurrentTest.Cancellation);
        await Task.WhenAll(second, third).WaitAsync(CurrentTest.Cancellation);

        Assert.AreEqual(2, backend.Reads);
    }

    [TestMethod]
    public async Task Disposing_does_not_wait_for_a_read_and_the_backend_closes_after_it()
    {
        var backend = new StubbornBackend();
        var loop = new ManualEventLoop();
        var service = new BluetoothService(loop, new Log(), backend);
        var read = service.RefreshAsync();
        await backend.Started.Task.WaitAsync(CurrentTest.Cancellation);

        service.Stop();
        Assert.IsFalse(backend.Disposed);
        backend.Finish.TrySetResult(Snapshot());
        await read.WaitAsync(CurrentTest.Cancellation);
        loop.Process();

        Assert.IsTrue(backend.Disposed);
        Assert.IsFalse(backend.DisposedDuringRead);
        Assert.IsFalse(service.Ready);
    }

    private sealed class Session : IDisposable
    {
        private Session()
        {
            Service = new BluetoothService(Loop, new Log(), Backend);
        }

        public FakeBackend Backend { get; } = new();

        public ManualEventLoop Loop { get; } = new();

        public BluetoothService Service { get; }

        public BluetoothDevice Device => Assert.ContainsSingle(Service.Devices);

        public static async Task<Session> OpenAsync(BluetoothSnapshot snapshot)
        {
            var session = new Session();
            await session.ShowAsync(snapshot);
            return session;
        }

        public async Task ShowAsync(BluetoothSnapshot snapshot)
        {
            Backend.Current = snapshot;
            await Service.RefreshAsync();
            Loop.Process();
        }

        public void Dispose()
        {
            Service.Stop();
        }
    }

    private abstract class ReadBackend : IBluetoothBackend
    {
        public event Action? Changed;

        public bool Disposed { get; private set; }

        public abstract Task<BluetoothSnapshot> ReadAsync(CancellationToken cancellation);

        public void RaiseChanged()
        {
            Changed?.Invoke();
        }

        public void StartWatching()
        {
        }

        public void StopWatching()
        {
        }

        public void StartDiscovery()
        {
        }

        public void StopDiscovery()
        {
        }

        public Task<string?> PairAsync(string id, CancellationToken cancellation)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> UnpairAsync(string id)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<bool> SetAudioConnectionAsync(Guid container, bool connect)
        {
            return Task.FromResult(true);
        }

        public Task<string?> SetDiscoverableAsync(bool discoverable)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> SetPairableAsync(bool pairable)
        {
            return Task.FromResult<string?>(null);
        }

        public virtual void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class FlakyBackend : ReadBackend
    {
        private int _reads;

        public override Task<BluetoothSnapshot> ReadAsync(CancellationToken cancellation)
        {
            if (Interlocked.Increment(ref _reads) == 1)
                return Task.FromException<BluetoothSnapshot>(new InvalidCastException("radio broke"));
            return Task.FromResult(Snapshot());
        }
    }

    private sealed class TokenBackend : ReadBackend
    {
        private int _finished;

        public int Finished => Volatile.Read(ref _finished);

        public override Task<BluetoothSnapshot> ReadAsync(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _finished);
            return Task.FromResult(Snapshot());
        }
    }

    private sealed class GatedBackend : ReadBackend
    {
        private int _reads;

        public int Reads => Volatile.Read(ref _reads);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Gate { get; } = new();

        public override async Task<BluetoothSnapshot> ReadAsync(CancellationToken cancellation)
        {
            Interlocked.Increment(ref _reads);
            Started.TrySetResult();
            await Gate.Task.WaitAsync(cancellation).ConfigureAwait(false);
            return Snapshot();
        }
    }

    private sealed class StubbornBackend : ReadBackend
    {
        private volatile bool _reading;

        public bool DisposedDuringRead { get; private set; }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<BluetoothSnapshot> Finish { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<BluetoothSnapshot> ReadAsync(CancellationToken cancellation)
        {
            _reading = true;
            Started.TrySetResult();
            var snapshot = await Finish.Task.ConfigureAwait(false);
            _reading = false;
            return snapshot;
        }

        public override void Dispose()
        {
            DisposedDuringRead |= _reading;
            base.Dispose();
        }
    }

    private sealed class FakeBackend : IBluetoothBackend
    {
        private int _reads;

        public BluetoothSnapshot Current { get; set; } = Snapshot();

        public Task<BluetoothSnapshot>? ReadTask { get; set; }

        public Exception? Failure { get; set; }

        public Exception? ReadFailure { get; set; }

        public string? PairError { get; set; }

        public bool AudioAccepted { get; set; } = true;

        public bool WaitForPairCancellation { get; set; }

        public List<string> Calls { get; } = [];

        public List<(Guid, bool)> Connections { get; } = [];

        public List<string> Paired { get; } = [];

        public List<string> Unpaired { get; } = [];

        public int Reads => Volatile.Read(ref _reads);

        public bool Disposed { get; private set; }

        public string? DiscoverableError { get; set; }

        public string? PairableError { get; set; }

        public TaskCompletionSource<string?>? VisibilityGate { get; set; }

        public bool Watching { get; private set; }

        public event Action? Changed;

        public void RaiseChanged()
        {
            Changed?.Invoke();
        }

        public void StartWatching()
        {
            Watching = true;
        }

        public void StopWatching()
        {
            Watching = false;
        }

        public Task<BluetoothSnapshot> ReadAsync(CancellationToken cancellation)
        {
            Interlocked.Increment(ref _reads);
            if (ReadFailure is { } e)
                return Task.FromException<BluetoothSnapshot>(e);
            if (ReadTask is { } task)
                return task;
            return Task.FromResult(Current.Adapter is { } adapter
                ? Current with { Adapter = adapter with { Radio = RadioReading.Read(adapter.Radio.Radios) } }
                : Current);
        }

        public void StartDiscovery()
        {
            Calls.Add("start");
            Current = Current with { Discovering = true };
        }

        public void StopDiscovery()
        {
            Calls.Add("stop");
            Current = Current with { Discovering = false };
        }

        public async Task<string?> PairAsync(string id, CancellationToken cancellation)
        {
            Calls.Add("pair");
            Paired.Add(id);
            if (WaitForPairCancellation)
                await new TaskCompletionSource().Task.WaitAsync(cancellation);
            if (Failure is { } e)
                throw e;
            return PairError;
        }

        public Task<string?> UnpairAsync(string id)
        {
            Calls.Add("unpair");
            Unpaired.Add(id);
            return Failure is { } e ? Task.FromException<string?>(e) : Task.FromResult<string?>(null);
        }

        public Task<bool> SetAudioConnectionAsync(Guid container, bool connect)
        {
            Calls.Add(connect ? "connect" : "disconnect");
            Connections.Add((container, connect));
            return Failure is { } e ? Task.FromException<bool>(e) : Task.FromResult(AudioAccepted);
        }

        public Task<string?> SetDiscoverableAsync(bool discoverable)
        {
            return Visibility(discoverable ? "discoverable-on" : "discoverable-off", () =>
            {
                if (DiscoverableError is { } error)
                    return error;
                if (Current.Adapter is { } adapter)
                    Current = Current with { Adapter = adapter with { Discoverable = discoverable } };
                return null;
            });
        }

        public Task<string?> SetPairableAsync(bool pairable)
        {
            return Visibility(pairable ? "pairable-on" : "pairable-off", () =>
            {
                if (PairableError is { } error)
                    return error;
                if (Current.Adapter is { } adapter)
                    Current = Current with { Adapter = adapter with { Pairable = pairable } };
                return null;
            });
        }

        private Task<string?> Visibility(string call, Func<string?> apply)
        {
            Calls.Add(call);
            if (VisibilityGate is { } gate)
                return FinishVisibility(gate.Task, apply);
            return Task.FromResult(apply());
        }

        private static async Task<string?> FinishVisibility(Task<string?> pending, Func<string?> apply)
        {
            var error = await pending.ConfigureAwait(false);
            return error ?? apply();
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
