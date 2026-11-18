using System.ComponentModel;
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
    public void Property_keys_keep_the_spellings_a_shell_reads()
    {
        Assert.AreEqual("endpointId", AudioProperty.EndpointId);
        Assert.AreEqual("friendlyName", AudioProperty.FriendlyName);
        Assert.AreEqual("interfaceName", AudioProperty.InterfaceName);
        Assert.AreEqual("flow", AudioProperty.Flow);
        Assert.AreEqual("adapter", AudioProperty.Adapter);
        Assert.AreEqual("icon", AudioProperty.Icon);
        Assert.AreEqual("formFactor", AudioProperty.FormFactor);
        Assert.AreEqual("sessionId", AudioProperty.SessionId);
        Assert.AreEqual("appName", AudioProperty.AppName);
        Assert.AreEqual("processId", AudioProperty.ProcessId);
        Assert.AreEqual("processName", AudioProperty.ProcessName);
    }

    [TestMethod]
    public void The_nodes_already_there_are_listed_and_a_session_knows_its_device()
    {
        var speaker = _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackDevice, "Speakers"));
        _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackSession, "App", device: speaker));

        using var audio = Service();

        Assert.AreSequenceEqual(["Speakers", "App"], audio.Nodes.Select(n => n.Description));
        Assert.AreSame(audio.Nodes[0], audio.Nodes[1].Device);
        Assert.IsNull(audio.Nodes[0].Device);
    }

    [TestMethod]
    public void A_node_that_stays_is_the_same_object()
    {
        var speaker = _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackDevice));
        var app = _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackSession, device: speaker));
        using var audio = Service();
        var kept = audio.Nodes[0];

        _graph.Remove(app);
        _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackSession, device: speaker));

        Assert.AreEqual(2, audio.Nodes.Count);
        Assert.AreSame(kept, audio.Nodes[0]);
        Assert.AreSame(kept, audio.Nodes[1].Device);
    }

    [TestMethod]
    public void A_node_says_what_it_is()
    {
        AudioChannel[] channels = [AudioChannel.FrontLeft, AudioChannel.FrontRight, AudioChannel.Unknown];
        var app = _graph.Add(new FakeAudioNode(AudioNodeType.RecordingSession, "App", channels));
        _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackDevice, "Speakers"));
        using var audio = Service();

        Assert.AreEqual(app.Id, audio.Nodes[0].Id);
        Assert.IsTrue(audio.Nodes[0].IsSession);
        Assert.IsFalse(audio.Nodes[0].IsPlayback);
        Assert.AreSequenceEqual([AudioChannel.FrontLeft, AudioChannel.FrontRight, AudioChannel.Unknown], audio.Nodes[0].Channels);
        Assert.IsFalse(audio.Nodes[1].IsSession);
        Assert.IsTrue(audio.Nodes[1].IsPlayback);
    }

    [TestMethod]
    public void A_new_name_is_reported()
    {
        var app = _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackSession, "App"));
        using var audio = Service();
        var changes = Watch(audio.Nodes[0]);

        app.Rename("Player");

        Assert.AreEqual("Player", audio.Nodes[0].Description);
        Assert.AreSequenceEqual([nameof(AudioNode.Properties), nameof(AudioNode.Description)], changes);
    }

    [TestMethod]
    public void The_defaults_follow_Windows_and_become_null_with_no_device()
    {
        var speaker = _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackDevice));
        var microphone = _graph.Add(new FakeAudioNode(AudioNodeType.RecordingDevice));
        _graph.SetDefaults(speaker, null);
        using var audio = Service();
        Assert.AreSame(audio.Nodes[0], audio.DefaultPlaybackDevice);
        Assert.IsNull(audio.DefaultRecordingDevice);
        var changes = Watch(audio);

        _graph.SetDefaults(speaker, microphone);
        Assert.AreSame(audio.Nodes[1], audio.DefaultRecordingDevice);
        Assert.AreSequenceEqual([nameof(AudioService.DefaultRecordingDevice)], changes);

        _graph.SetDefaults(null, microphone);
        Assert.IsNull(audio.DefaultPlaybackDevice);
    }

    [TestMethod]
    public void Setting_a_default_asks_Windows_and_shows_it_when_Windows_says_so()
    {
        var speaker = _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackDevice, "Endpoint"));
        speaker.Rename("Speakers");
        using var audio = Service();

        audio.DefaultPlaybackDevice = audio.Nodes[0];

        Assert.AreEqual("Endpoint", Assert.ContainsSingle(_graph.DefaultsAsked));
        Assert.AreEqual("Speakers", audio.Nodes[0].Description);
        Assert.IsNull(audio.DefaultPlaybackDevice);
    }

    [TestMethod]
    [DataRow((int)AudioNodeType.RecordingDevice, true)]
    [DataRow((int)AudioNodeType.PlaybackSession, true)]
    [DataRow((int)AudioNodeType.PlaybackDevice, false)]
    [DataRow((int)AudioNodeType.RecordingSession, false)]
    public void A_node_of_the_wrong_kind_cannot_be_a_default(int type, bool playback)
    {
        _graph.Add(new FakeAudioNode((AudioNodeType)type));
        using var audio = Service();

        if (playback)
            audio.DefaultPlaybackDevice = audio.Nodes[0];
        else
            audio.DefaultRecordingDevice = audio.Nodes[0];

        Assert.IsEmpty(_graph.DefaultsAsked);
        Assert.Contains("it is not a", Assert.ContainsSingle(_warnings), StringComparison.Ordinal);
    }

    [TestMethod]
    public void Setting_no_default_changes_nothing()
    {
        using var audio = Service();

        audio.DefaultPlaybackDevice = null;

        Assert.IsEmpty(_graph.DefaultsAsked);
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public void Volume_is_the_average_and_setting_it_keeps_the_balance()
    {
        var speaker = _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackDevice));
        speaker.SetAudio([0.25f, 0.5f], false);
        using var audio = Service();
        var volume = audio.Nodes[0];
        Assert.AreEqual(0.375, volume.Volume, 0.000005);

        volume.Volume = 0.75;

        Assert.AreEqual(0.5, volume.Volumes[0], 0.000005);
        Assert.AreEqual(1, volume.Volumes[1], 0.000005);
    }

    [TestMethod]
    public void Setting_the_volume_of_a_silent_node_sets_every_channel()
    {
        _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackDevice));
        using var audio = Service();

        audio.Nodes[0].Volume = 0.5;

        Assert.AreSequenceEqual([0.5, 0.5], audio.Nodes[0].Volumes);
    }

    [TestMethod]
    public void A_negative_channel_volume_is_zero()
    {
        _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackDevice));
        using var audio = Service();

        audio.Nodes[0].Volumes = [-1, 0.5];

        Assert.AreSequenceEqual([0, 0.5], audio.Nodes[0].Volumes);
    }

    [TestMethod]
    public void A_missing_or_empty_channel_list_leaves_the_levels()
    {
        var speaker = _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackDevice));
        speaker.SetAudio([0.25f, 0.5f], false);
        using var audio = Service();
        var levels = audio.Nodes[0];

        levels.Volumes = null!;
        Assert.AreSequenceEqual([0.25, 0.5], levels.Volumes);

        levels.Volumes = [];
        Assert.AreSequenceEqual([0.25, 0.5], levels.Volumes);
    }

    [TestMethod]
    public void A_channel_list_of_another_length_sets_every_channel_to_its_first()
    {
        _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackDevice));
        using var audio = Service();

        audio.Nodes[0].Volumes = [0.25, 0.5, 1];

        Assert.AreSequenceEqual([0.25, 0.25], audio.Nodes[0].Volumes);
    }

    [TestMethod]
    public void A_change_reports_only_the_properties_that_differ()
    {
        var speaker = _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackDevice));
        speaker.SetAudio([0.25f, 0.75f], false);
        using var audio = Service();
        var volume = audio.Nodes[0];
        var changes = Watch(volume);

        volume.Muted = true;
        Assert.IsTrue(volume.Muted);
        Assert.AreSequenceEqual([nameof(AudioNode.Muted)], changes);

        changes.Clear();
        speaker.SetAudio([0.75f, 0.25f], true);
        Assert.AreSequenceEqual([nameof(AudioNode.Volumes)], changes);

        changes.Clear();
        speaker.SetAudio([0.5f, 0.5f], true);
        Assert.AreSequenceEqual([nameof(AudioNode.Volumes)], changes);

        changes.Clear();
        speaker.SetAudio([0.25f, 0.25f], true);
        Assert.AreSequenceEqual([nameof(AudioNode.Volumes), nameof(AudioNode.Volume)], changes);
    }

    [TestMethod]
    public void A_volume_Windows_refuses_is_logged()
    {
        var speaker = _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackDevice, "Speakers"));
        using var audio = Service();
        speaker.FailWrites = true;

        audio.Nodes[0].Muted = true;

        Assert.IsFalse(audio.Nodes[0].Muted);
        Assert.Contains("Cannot change the volume of Speakers", Assert.ContainsSingle(_warnings), StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_session_says_when_it_starts_and_stops_playing()
    {
        var speaker = _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackDevice));
        var app = _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackSession, device: speaker));
        using var audio = Service();
        var session = audio.Nodes[1];
        Assert.IsFalse(session.Active);
        var changes = Watch(session);

        app.SetActive(true);

        Assert.IsTrue(session.Active);
        Assert.AreSequenceEqual([nameof(AudioNode.Active)], changes);
    }

    [TestMethod]
    public void A_disposed_service_stops_following_the_graph()
    {
        var speaker = _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackDevice));
        var app = _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackSession, device: speaker));
        var audio = Service();
        var deviceChanges = Watch(audio.Nodes[0]);
        var sessionChanges = Watch(audio.Nodes[1]);

        audio.Dispose();
        _graph.Add(new FakeAudioNode(AudioNodeType.RecordingDevice));
        speaker.SetAudio([1, 1], true);
        app.SetActive(true);

        Assert.AreEqual(2, audio.Nodes.Count);
        Assert.IsEmpty(deviceChanges);
        Assert.IsEmpty(sessionChanges);
    }

    private (AudioService Audio, FakeAudioNode Node, FakeMeter Meter) Metered(params float[] peaks)
    {
        var meter = new FakeMeter(peaks);
        var node = _graph.Add(new FakeAudioNode(AudioNodeType.RecordingDevice) { FakeMeter = meter });
        return (Service(), node, meter);
    }

    [TestMethod]
    public void A_peak_monitor_reads_at_once_and_about_30_times_a_second()
    {
        var (audio, _, meter) = Metered(0.25f, 0.5f);
        using var _ = audio;
        using var monitor = new AudioPeakMonitor(_loop);
        monitor.Node = audio.Nodes[0];

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
        using var _ = audio;
        using var monitor = new AudioPeakMonitor(_loop);
        monitor.Node = audio.Nodes[0];

        Assert.AreSequenceEqual([AudioChannel.Mono], monitor.Channels);
    }

    [TestMethod]
    public void A_disabled_peak_monitor_clears_and_stops_reading()
    {
        var (audio, node, meter) = Metered(0.25f, 0.5f);
        using var _ = audio;
        using var monitor = new AudioPeakMonitor(_loop);
        monitor.Node = audio.Nodes[0];
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
        _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackDevice));
        using var audio = Service();
        using var monitor = new AudioPeakMonitor(_loop);
        monitor.Node = audio.Nodes[0];

        Assert.AreEqual(0, monitor.Peak);
        Assert.IsNull(_loop.NextDue);
    }

    [TestMethod]
    public void A_peak_monitor_clears_when_its_node_is_removed()
    {
        var (audio, node, _) = Metered(0.25f, 0.5f);
        using var _ = audio;
        using var monitor = new AudioPeakMonitor(_loop);
        monitor.Node = audio.Nodes[0];

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
        using var _ = audio;
        using var monitor = new AudioPeakMonitor(_loop);
        monitor.Node = audio.Nodes[0];

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
        using var _ = audio;
        using var monitor = new AudioPeakMonitor(_loop);
        monitor.Node = audio.Nodes[0];
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
        using var _ = audio;
        var monitor = new AudioPeakMonitor(_loop) { Node = audio.Nodes[0] };

        monitor.Dispose();

        Assert.AreEqual(0, node.Running);
        Assert.IsNull(monitor.Node);
        Assert.IsNull(_loop.NextDue);
    }

    [TestMethod]
    public void A_device_that_leaves_stops_metering_once()
    {
        var (audio, node, meter) = Metered(0.25f, 0.5f);
        using var _ = audio;
        using var monitor = new AudioPeakMonitor(_loop);
        var device = audio.Nodes[0];
        monitor.Node = device;
        Assert.AreEqual(0.5, monitor.Peak);

        _graph.Remove(node);
        var reads = meter.Reads;
        _graph.Remove(node);
        _loop.Advance(TimeSpan.FromMilliseconds(40));

        Assert.DoesNotContain(item => item.Id == device.Id, audio.Nodes);
        Assert.AreEqual(0, monitor.Peak);
        Assert.AreEqual(0, node.Running);
        Assert.AreEqual(reads, meter.Reads);
        Assert.IsNull(_loop.NextDue);
        device.Muted = true;
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public void A_device_the_service_holds_still_meters_after_another_read()
    {
        var meter = new FakeMeter(0.25f, 0.5f);
        var speaker = _graph.Add(new FakeAudioNode(AudioNodeType.PlaybackDevice, "Speakers") { FakeMeter = meter });
        _graph.SetDefaults(speaker, null);
        using var audio = Service();
        using var monitor = new AudioPeakMonitor(_loop);
        var device = audio.DefaultPlaybackDevice;
        Assert.IsNotNull(device);
        monitor.Node = device;
        Assert.AreEqual(0.5, monitor.Peak);

        _graph.SetDefaults(speaker, null);
        _loop.Advance(TimeSpan.FromMilliseconds(40));

        Assert.AreSame(device, audio.DefaultPlaybackDevice);
        Assert.Contains(item => ReferenceEquals(item, device), audio.Nodes);
        Assert.AreEqual(0.5, monitor.Peak);
        Assert.IsTrue(meter.Reads > 1);
    }
}
