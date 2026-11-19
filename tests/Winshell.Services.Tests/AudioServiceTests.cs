using System.ComponentModel;
using Windows.Win32.Media.Audio;
using Winshell.Audio;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class AudioServiceTests
{
    private readonly ManualEventLoop _loop = new();
    private readonly FakeAudioBackend _graph = new();
    private readonly List<string> _warnings = [];
    private readonly Log _log;

    public AudioServiceTests()
    {
        _log = new Log { Sink = (_, _, message) => _warnings.Add(message) };
    }

    private AudioService Service()
    {
        return new AudioService(_loop, _log, _graph);
    }

    private static List<string> Watch(INotifyPropertyChanged source)
    {
        var changes = new List<string>();
        source.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);
        return changes;
    }

    [TestMethod]
    public void The_devices_already_there_are_listed_and_a_session_knows_its_device()
    {
        var speaker = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice, "Speakers"));
        _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackSession, "App", device: speaker));

        var audio = Service();
        using var stopAudio = audio.Stopping();

        var device = Assert.ContainsSingle(audio.PlaybackDevices);
        Assert.AreEqual("Speakers", device.Name);
        var session = Assert.ContainsSingle(device.Sessions);
        Assert.AreEqual("App", session.Name);
        Assert.AreSame(device, session.Device);
        Assert.IsEmpty(audio.RecordingDevices);
    }

    [TestMethod]
    public void A_device_that_stays_is_the_same_object()
    {
        var speaker = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice));
        var app = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackSession, device: speaker));
        var audio = Service();
        using var stopAudio = audio.Stopping();
        var kept = audio.PlaybackDevices[0];

        _graph.Remove(app);
        _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackSession, device: speaker));

        Assert.AreSame(kept, Assert.ContainsSingle(audio.PlaybackDevices));
        Assert.AreSame(kept, Assert.ContainsSingle(kept.Sessions).Device);
    }

    [TestMethod]
    public void Each_device_and_session_is_listed_under_its_kind()
    {
        AudioChannel[] channels = [AudioChannel.FrontLeft, AudioChannel.FrontRight, AudioChannel.Unknown];
        var microphone = _graph.Add(new FakeGraphNode(AudioNodeType.RecordingDevice, "Microphone"));
        _graph.Add(new FakeGraphNode(AudioNodeType.RecordingSession, "App", channels, microphone));
        _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice, "Speakers"));
        var audio = Service();
        using var stopAudio = audio.Stopping();

        var recording = Assert.ContainsSingle(audio.RecordingDevices);
        Assert.AreEqual("Microphone", recording.Name);
        var session = Assert.ContainsSingle(recording.Sessions);
        Assert.AreEqual("App", session.Name);
        Assert.AreSequenceEqual([AudioChannel.FrontLeft, AudioChannel.FrontRight, AudioChannel.Unknown], session.Channels);
        var playback = Assert.ContainsSingle(audio.PlaybackDevices);
        Assert.AreEqual("Speakers", playback.Name);
        Assert.IsEmpty(playback.Sessions);
    }

    [TestMethod]
    public void A_device_says_what_form_it_has()
    {
        _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice, formFactor: AudioFormFactor.Headphones));
        var audio = Service();
        using var stopAudio = audio.Stopping();

        Assert.AreEqual(AudioFormFactor.Headphones, audio.PlaybackDevices[0].FormFactor);
    }

    [TestMethod]
    [DataRow((uint)EndpointFormFactor.RemoteNetworkDevice, AudioFormFactor.Network)]
    [DataRow((uint)EndpointFormFactor.Speakers, AudioFormFactor.Speakers)]
    [DataRow((uint)EndpointFormFactor.LineLevel, AudioFormFactor.Line)]
    [DataRow((uint)EndpointFormFactor.Headphones, AudioFormFactor.Headphones)]
    [DataRow((uint)EndpointFormFactor.Microphone, AudioFormFactor.Microphone)]
    [DataRow((uint)EndpointFormFactor.Headset, AudioFormFactor.Headset)]
    [DataRow((uint)EndpointFormFactor.Handset, AudioFormFactor.Handset)]
    [DataRow((uint)EndpointFormFactor.UnknownDigitalPassthrough, AudioFormFactor.Unknown)]
    [DataRow((uint)EndpointFormFactor.SPDIF, AudioFormFactor.Spdif)]
    [DataRow((uint)EndpointFormFactor.DigitalAudioDisplayDevice, AudioFormFactor.Hdmi)]
    [DataRow((uint)EndpointFormFactor.UnknownFormFactor, AudioFormFactor.Unknown)]
    public void A_windows_endpoint_form_factor_becomes_its_audio_form_factor(uint windows, AudioFormFactor expected)
    {
        Assert.AreEqual(expected, EndpointNode.FormFactorOf(windows));
    }

    [TestMethod]
    public void An_endpoint_without_a_form_factor_is_unknown()
    {
        Assert.AreEqual(AudioFormFactor.Unknown, EndpointNode.FormFactorOf(null));
    }

    [TestMethod]
    public void A_new_name_is_reported()
    {
        var speaker = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice));
        var app = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackSession, "App", device: speaker));
        var audio = Service();
        using var stopAudio = audio.Stopping();
        var session = audio.PlaybackDevices[0].Sessions[0];
        var changes = Watch(session);

        app.Rename("Player");

        Assert.AreEqual("Player", session.Name);
        Assert.AreSequenceEqual([nameof(AudioSession.Name)], changes);
    }

    [TestMethod]
    public void The_defaults_follow_Windows_and_become_null_with_no_device()
    {
        var speaker = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice));
        var microphone = _graph.Add(new FakeGraphNode(AudioNodeType.RecordingDevice));
        _graph.SetDefaults(speaker, null);
        var audio = Service();
        using var stopAudio = audio.Stopping();
        Assert.AreSame(audio.PlaybackDevices[0], audio.DefaultPlaybackDevice);
        Assert.IsNull(audio.DefaultRecordingDevice);
        var changes = Watch(audio);

        _graph.SetDefaults(speaker, microphone);
        Assert.AreSame(audio.RecordingDevices[0], audio.DefaultRecordingDevice);
        Assert.AreSequenceEqual([nameof(AudioService.DefaultRecordingDevice)], changes);

        _graph.SetDefaults(null, microphone);
        Assert.IsNull(audio.DefaultPlaybackDevice);
    }

    [TestMethod]
    public void Setting_a_default_asks_Windows_and_shows_it_when_Windows_says_so()
    {
        var speaker = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice, "Endpoint"));
        speaker.Rename("Speakers");
        var audio = Service();
        using var stopAudio = audio.Stopping();

        audio.DefaultPlaybackDevice = audio.PlaybackDevices[0];

        Assert.AreEqual("Endpoint", Assert.ContainsSingle(_graph.DefaultsAsked));
        Assert.AreEqual("Speakers", audio.PlaybackDevices[0].Name);
        Assert.IsNull(audio.DefaultPlaybackDevice);
    }

    [TestMethod]
    public void Setting_no_default_changes_nothing()
    {
        var audio = Service();
        using var stopAudio = audio.Stopping();

        audio.DefaultPlaybackDevice = null;

        Assert.IsEmpty(_graph.DefaultsAsked);
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public void Volume_is_the_average_and_setting_it_keeps_the_balance()
    {
        var speaker = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice));
        speaker.SetAudio([0.25f, 0.5f], false);
        var audio = Service();
        using var stopAudio = audio.Stopping();
        var volume = audio.PlaybackDevices[0];
        Assert.AreEqual(0.375, volume.Volume, 5);

        volume.Volume = 0.75;

        Assert.AreEqual(0.5, volume.Volumes[0], 0.000005);
        Assert.AreEqual(1, volume.Volumes[1], 0.000005);
    }

    [TestMethod]
    public void Setting_the_volume_of_a_silent_node_sets_every_channel()
    {
        _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice));
        var audio = Service();
        using var stopAudio = audio.Stopping();

        audio.PlaybackDevices[0].Volume = 0.5;

        Assert.AreSequenceEqual([0.5, 0.5], audio.PlaybackDevices[0].Volumes);
    }

    [TestMethod]
    public void A_negative_channel_volume_is_zero()
    {
        _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice));
        var audio = Service();
        using var stopAudio = audio.Stopping();

        audio.PlaybackDevices[0].Volumes = [-1, 0.5];

        Assert.AreSequenceEqual([0, 0.5], audio.PlaybackDevices[0].Volumes);
    }

    [TestMethod]
    public void A_missing_or_empty_channel_list_leaves_the_levels()
    {
        var speaker = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice));
        speaker.SetAudio([0.25f, 0.5f], false);
        var audio = Service();
        using var stopAudio = audio.Stopping();
        var levels = audio.PlaybackDevices[0];

        levels.Volumes = null!;
        Assert.AreSequenceEqual([0.25, 0.5], levels.Volumes);

        levels.Volumes = [];
        Assert.AreSequenceEqual([0.25, 0.5], levels.Volumes);
    }

    [TestMethod]
    public void A_channel_list_of_another_length_sets_every_channel_to_its_first()
    {
        _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice));
        var audio = Service();
        using var stopAudio = audio.Stopping();

        audio.PlaybackDevices[0].Volumes = [0.25, 0.5, 1];

        Assert.AreSequenceEqual([0.25, 0.25], audio.PlaybackDevices[0].Volumes);
    }

    [TestMethod]
    public void A_change_reports_only_the_properties_that_differ()
    {
        var speaker = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice));
        speaker.SetAudio([0.25f, 0.75f], false);
        var audio = Service();
        using var stopAudio = audio.Stopping();
        var volume = audio.PlaybackDevices[0];
        var changes = Watch(volume);

        volume.Muted = true;
        Assert.IsTrue(volume.Muted);
        Assert.AreSequenceEqual([nameof(AudioVolume.Muted)], changes);

        changes.Clear();
        speaker.SetAudio([0.75f, 0.25f], true);
        Assert.AreSequenceEqual([nameof(AudioVolume.Volumes)], changes);

        changes.Clear();
        speaker.SetAudio([0.5f, 0.5f], true);
        Assert.AreSequenceEqual([nameof(AudioVolume.Volumes)], changes);

        changes.Clear();
        speaker.SetAudio([0.25f, 0.25f], true);
        Assert.AreSequenceEqual([nameof(AudioVolume.Volumes), nameof(AudioVolume.Volume)], changes);
    }

    [TestMethod]
    public void A_volume_Windows_refuses_is_logged()
    {
        var speaker = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice, "Speakers"));
        var audio = Service();
        using var stopAudio = audio.Stopping();
        speaker.FailWrites = true;

        audio.PlaybackDevices[0].Muted = true;

        Assert.IsFalse(audio.PlaybackDevices[0].Muted);
        Assert.Contains("Cannot change the volume of Speakers", Assert.ContainsSingle(_warnings), StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_session_says_when_it_starts_and_stops_playing()
    {
        var speaker = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice));
        var app = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackSession, device: speaker));
        var audio = Service();
        using var stopAudio = audio.Stopping();
        var session = audio.PlaybackDevices[0].Sessions[0];
        Assert.IsFalse(session.Active);
        var changes = Watch(session);

        app.SetActive(true);

        Assert.IsTrue(session.Active);
        Assert.AreSequenceEqual([nameof(AudioSession.Active)], changes);
    }

    [TestMethod]
    public void A_disposed_service_stops_following_the_graph()
    {
        var speaker = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice));
        var app = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackSession, device: speaker));
        var audio = Service();
        var deviceChanges = Watch(audio.PlaybackDevices[0]);
        var sessionChanges = Watch(audio.PlaybackDevices[0].Sessions[0]);

        audio.Stop();
        _graph.Add(new FakeGraphNode(AudioNodeType.RecordingDevice));
        speaker.SetAudio([1, 1], true);
        app.SetActive(true);

        Assert.ContainsSingle(audio.PlaybackDevices);
        Assert.ContainsSingle(audio.PlaybackDevices[0].Sessions);
        Assert.IsEmpty(audio.RecordingDevices);
        Assert.IsEmpty(deviceChanges);
        Assert.IsEmpty(sessionChanges);
    }

    private (AudioService Audio, FakeGraphNode Node, FakeMeter Meter) Metered(params float[] peaks)
    {
        var meter = new FakeMeter(peaks);
        var node = _graph.Add(new FakeGraphNode(AudioNodeType.RecordingDevice) { FakeMeter = meter });
        return (Service(), node, meter);
    }

    [TestMethod]
    public void A_peak_monitor_reads_at_once_and_about_30_times_a_second()
    {
        var (audio, _, meter) = Metered(0.25f, 0.5f);
        using var _ = audio.Stopping();
        using var monitor = new AudioPeakMonitor(_loop);
        monitor.Audio = audio.RecordingDevices[0];

        Assert.AreEqual(0.5, monitor.Peak);
        Assert.AreSequenceEqual([0.25, 0.5], monitor.Peaks);
        Assert.AreSequenceEqual([AudioChannel.FrontLeft, AudioChannel.FrontRight], monitor.Channels);

        var reads = meter.Reads;
        meter.Peaks = [0.75f, 0.5f];
        _loop.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(0.75, monitor.Peak);
        Assert.IsInRange(25, 31, meter.Reads - reads);
    }

    [TestMethod]
    public void A_peak_monitor_names_channels_itself_when_the_meter_has_another_count()
    {
        var (audio, _, _) = Metered(0.25f);
        using var _ = audio.Stopping();
        using var monitor = new AudioPeakMonitor(_loop);
        monitor.Audio = audio.RecordingDevices[0];

        Assert.AreSequenceEqual([AudioChannel.Mono], monitor.Channels);
    }

    [TestMethod]
    public void A_disabled_peak_monitor_clears_and_stops_reading()
    {
        var (audio, node, meter) = Metered(0.25f, 0.5f);
        using var _ = audio.Stopping();
        using var monitor = new AudioPeakMonitor(_loop);
        monitor.Audio = audio.RecordingDevices[0];
        Assert.AreEqual(1, node.Running);

        monitor.Enabled = false;

        Assert.AreEqual(0, monitor.Peak);
        Assert.IsEmpty(monitor.Peaks);
        Assert.IsEmpty(monitor.Channels);
        Assert.AreEqual(0, node.Running);
        Assert.IsNull(_loop.NextDue);

        var reads = meter.Reads;
        monitor.Enabled = true;
        Assert.AreEqual(reads + 1, meter.Reads);
        Assert.AreEqual(0.5, monitor.Peak);
    }

    [TestMethod]
    public void A_peak_monitor_on_a_node_with_no_meter_stays_at_zero()
    {
        _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice));
        var audio = Service();
        using var stopAudio = audio.Stopping();
        using var monitor = new AudioPeakMonitor(_loop);
        monitor.Audio = audio.PlaybackDevices[0];

        Assert.AreEqual(0, monitor.Peak);
        Assert.IsNull(_loop.NextDue);
    }

    [TestMethod]
    public void A_peak_monitor_clears_when_its_node_is_removed()
    {
        var (audio, node, _) = Metered(0.25f, 0.5f);
        using var _ = audio.Stopping();
        using var monitor = new AudioPeakMonitor(_loop);
        monitor.Audio = audio.RecordingDevices[0];

        _graph.Remove(node);

        Assert.AreEqual(0, monitor.Peak);
        Assert.IsEmpty(monitor.Peaks);
        Assert.AreEqual(0, node.Running);
        Assert.IsNull(_loop.NextDue);
    }

    [TestMethod]
    public void A_peak_monitor_keeps_reading_when_the_device_fails_a_read()
    {
        var (audio, _, meter) = Metered(0.25f, 0.5f);
        using var _ = audio.Stopping();
        using var monitor = new AudioPeakMonitor(_loop);
        monitor.Audio = audio.RecordingDevices[0];

        meter.Gone = true;
        _loop.Advance(TimeSpan.FromMilliseconds(40));
        meter.Gone = false;
        meter.Peaks = [0.75f, 0.5f];
        _loop.Advance(TimeSpan.FromMilliseconds(40));

        Assert.AreEqual(0.75, monitor.Peak);
    }

    [TestMethod]
    public void A_handler_that_turns_the_monitor_off_stops_the_next_read()
    {
        var (audio, _, meter) = Metered(0.25f, 0.5f);
        using var _ = audio.Stopping();
        using var monitor = new AudioPeakMonitor(_loop);
        monitor.Audio = audio.RecordingDevices[0];
        monitor.PropertyChanged += (_, _) => monitor.Enabled = false;

        meter.Peaks = [0.75f, 0.5f];
        _loop.Advance(TimeSpan.FromMilliseconds(40));

        Assert.IsFalse(monitor.Enabled);
        Assert.IsNull(_loop.NextDue);
    }

    [TestMethod]
    public void A_disposed_peak_monitor_lets_go_of_the_device()
    {
        var (audio, node, _) = Metered(0.25f, 0.5f);
        using var _ = audio.Stopping();
        var monitor = new AudioPeakMonitor(_loop) { Audio = audio.RecordingDevices[0] };

        monitor.Dispose();

        Assert.AreEqual(0, node.Running);
        Assert.IsNull(monitor.Audio);
        Assert.IsNull(_loop.NextDue);
    }

    [TestMethod]
    public void A_device_that_leaves_stops_metering_once()
    {
        var (audio, node, meter) = Metered(0.25f, 0.5f);
        using var _ = audio.Stopping();
        using var monitor = new AudioPeakMonitor(_loop);
        var device = audio.RecordingDevices[0];
        monitor.Audio = device;
        Assert.AreEqual(0.5, monitor.Peak);

        _graph.Remove(node);
        var reads = meter.Reads;
        _graph.Remove(node);
        _loop.Advance(TimeSpan.FromMilliseconds(40));

        Assert.IsEmpty(audio.RecordingDevices);
        Assert.AreEqual(0, monitor.Peak);
        Assert.AreEqual(0, node.Running);
        Assert.AreEqual(reads, meter.Reads);
        Assert.IsNull(_loop.NextDue);
        device.Muted = true;
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public void A_session_stops_metering_when_its_device_leaves()
    {
        var meter = new FakeMeter(0.25f, 0.5f);
        var speaker = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice));
        var app = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackSession, device: speaker) { FakeMeter = meter });
        var audio = Service();
        using var stopAudio = audio.Stopping();
        using var monitor = new AudioPeakMonitor(_loop);
        monitor.Audio = audio.PlaybackDevices[0].Sessions[0];
        Assert.AreEqual(0.5, monitor.Peak);

        _graph.Remove(speaker);

        Assert.IsEmpty(audio.PlaybackDevices);
        Assert.AreEqual(0, monitor.Peak);
        Assert.AreEqual(0, app.Running);
        Assert.IsNull(_loop.NextDue);
    }

    [TestMethod]
    public void A_device_the_service_holds_still_meters_after_another_read()
    {
        var meter = new FakeMeter(0.25f, 0.5f);
        var speaker = _graph.Add(new FakeGraphNode(AudioNodeType.PlaybackDevice, "Speakers") { FakeMeter = meter });
        _graph.SetDefaults(speaker, null);
        var audio = Service();
        using var stopAudio = audio.Stopping();
        using var monitor = new AudioPeakMonitor(_loop);
        var device = audio.DefaultPlaybackDevice;
        Assert.IsNotNull(device);
        monitor.Audio = device;
        Assert.AreEqual(0.5, monitor.Peak);

        _graph.SetDefaults(speaker, null);
        _loop.Advance(TimeSpan.FromMilliseconds(40));

        Assert.AreSame(device, audio.DefaultPlaybackDevice);
        Assert.Contains(device, audio.PlaybackDevices);
        Assert.AreEqual(0.5, monitor.Peak);
        Assert.IsTrue(meter.Reads > 1);
    }
}
