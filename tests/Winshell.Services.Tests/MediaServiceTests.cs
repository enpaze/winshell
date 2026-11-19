using System.ComponentModel;
using Windows.Foundation;
using Windows.Media.Control;
using Winshell.Media;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class MediaServiceTests
{
    private const GlobalSystemMediaTransportControlsSessionPlaybackStatus Playing =
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

    private const GlobalSystemMediaTransportControlsSessionPlaybackStatus Paused =
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused;

    private const GlobalSystemMediaTransportControlsSessionPlaybackStatus Stopped =
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped;

    private const GlobalSystemMediaTransportControlsSessionPlaybackStatus Changing =
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing;

    private readonly List<string> _warnings = [];
    private readonly Log _log;
    private readonly ManualEventLoop _loop = new();
    private readonly RecordingLink _link = new();
    private readonly SmtcSession _native;

    public MediaServiceTests()
    {
        _log = new Log { Sink = (_, _, message) => _warnings.Add(message) };
        _native = Native(_link);
    }

    private SmtcSession Native(RecordingLink link, string appId = "test")
    {
        return new SmtcSession(_loop, _log, appId, appId, link);
    }

    private sealed class RecordingLink : ISmtcLink
    {
        public List<string> Sent { get; } = [];

        public void Start(SmtcSession target)
        {
        }

        public void Retarget(GlobalSystemMediaTransportControlsSession session)
        {
        }

        public void Release()
        {
        }

        public void Send(string what, Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> command)
        {
            Sent.Add(what);
        }
    }

    private static void Controls(SmtcSession native, bool canPlay = false, bool canPause = false, bool canStop = false,
        bool canGoNext = false, bool canGoPrevious = false, bool canSeek = false)
    {
        native.ApplyPlayback(new SmtcPlayback(Stopped)
        {
            CanPlay = canPlay,
            CanPause = canPause,
            CanStop = canStop,
            CanGoNext = canGoNext,
            CanGoPrevious = canGoPrevious,
            CanSeek = canSeek
        });
    }

    private sealed class FakeSessions(Func<string, SmtcSession> create) : IMediaBackend
    {
        private readonly List<SmtcSession> _sessions = [];

        public IReadOnlyList<SmtcSession> Sessions => _sessions;

        public bool Disposed { get; private set; }

        public event Action<SmtcSession>? SessionAdded;

        public event Action<SmtcSession>? SessionRemoved;

        public SmtcSession Add(string appId)
        {
            var session = create(appId);
            _sessions.Add(session);
            SessionAdded?.Invoke(session);
            return session;
        }

        public void Remove(SmtcSession session)
        {
            _sessions.Remove(session);
            SessionRemoved?.Invoke(session);
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private MediaSession Session()
    {
        return new MediaSession(_native, _loop, _log);
    }

    private void Track(string title, string artist = "", string album = "", string albumArtist = "", int number = 0,
        string[]? genres = null)
    {
        _native.ApplyTrack(title, artist, album, albumArtist, number, genres, null);
    }

    private void Timeline(double position, double end)
    {
        _native.ApplyTimeline(TimeSpan.Zero, TimeSpan.FromSeconds(end), TimeSpan.FromSeconds(position), default,
            TimeProvider.System.GetUtcNow());
    }

    private static List<string> Watch(INotifyPropertyChanged source)
    {
        var changes = new List<string>();
        source.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);
        return changes;
    }

    [TestMethod]
    public void The_sessions_follow_Windows_and_one_that_stays_is_the_same_object()
    {
        var sessions = new FakeSessions(id => Native(new RecordingLink(), id));
        var first = sessions.Add("a.exe");
        var media = new MediaService(new ManualEventLoop(), _log, sessions);
        using var stopMedia = media.Stopping();
        var second = sessions.Add("b.exe");
        var kept = media.Sessions[1];

        sessions.Remove(first);

        Assert.AreSame(kept, Assert.ContainsSingle(media.Sessions));
        Assert.AreEqual("b.exe", kept.AppId);
        Assert.AreSame(second, kept.Native);
    }

    [TestMethod]
    public void A_session_that_left_stops_reporting()
    {
        var sessions = new FakeSessions(id => Native(new RecordingLink(), id));
        var native = sessions.Add("a.exe");
        var media = new MediaService(new ManualEventLoop(), _log, sessions);
        using var stopMedia = media.Stopping();
        var changes = Watch(media.Sessions[0]);

        sessions.Remove(native);
        Controls(native, true);

        Assert.IsEmpty(changes);
    }

    [TestMethod]
    public void A_disposed_service_stops_following_Windows()
    {
        var sessions = new FakeSessions(id => Native(new RecordingLink(), id));
        var native = sessions.Add("a.exe");
        var media = new MediaService(new ManualEventLoop(), _log, sessions);
        var changes = Watch(media.Sessions[0]);

        media.Stop();
        sessions.Add("b.exe");
        Controls(native, true);

        Assert.ContainsSingle(media.Sessions);
        Assert.IsEmpty(changes);
        Assert.IsTrue(sessions.Disposed);
    }

    [TestMethod]
    public void Can_control_is_false_with_no_command_true_with_one_and_false_when_it_goes_away()
    {
        var session = Session();
        var changes = Watch(session);

        int Count()
        {
            return changes.Count(c => c == nameof(MediaSession.CanControl));
        }

        Assert.IsFalse(session.CanControl);

        Controls(_native, canPause: true);
        Assert.IsTrue(session.CanControl);
        Assert.AreEqual(1, Count());

        Controls(_native, canPause: true, canGoNext: true);
        Controls(_native, canGoNext: true);
        Assert.IsTrue(session.CanControl);
        Assert.AreEqual(1, Count());

        Controls(_native);
        Assert.IsFalse(session.CanControl);
        Assert.AreEqual(2, Count());

        Controls(_native, canSeek: true);
        Assert.IsTrue(session.CanControl);
        Assert.AreEqual(3, Count());
    }

    [TestMethod]
    public void Can_control_reports_the_loss_of_a_command_the_session_started_with()
    {
        Controls(_native, true);
        var session = Session();
        var changes = Watch(session);
        Assert.IsTrue(session.CanControl);

        Controls(_native);

        Assert.IsFalse(session.CanControl);
        Assert.Contains(nameof(MediaSession.CanControl), changes);
    }

    [TestMethod]
    public void A_property_read_from_others_is_raised_when_they_change()
    {
        var changes = Watch(Session());

        void Raised(params string[] names)
        {
            Assert.ContainsAll(names, changes);
            changes.Clear();
        }

        Track("Song", "Artist", "Album", "Band", 3, ["Rock"]);
        Raised(nameof(MediaSession.Metadata));

        _native.ApplyPlayback(new SmtcPlayback(Playing) { CanPause = true, RateSupported = true });
        Raised(nameof(MediaSession.IsPlaying), nameof(MediaSession.CanTogglePlaying), nameof(MediaSession.MinRate),
            nameof(MediaSession.MaxRate));

        Timeline(5, 125);
        Raised(nameof(MediaSession.LengthSupported), nameof(MediaSession.Metadata));
    }

    [TestMethod]
    public void A_new_track_is_told_before_and_after_its_properties_change()
    {
        var session = Session();
        var heard = new List<string>();
        session.TrackChanged += () => heard.Add("before");
        session.PostTrackChanged += () => heard.Add("after");
        session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MediaSession.TrackTitle))
                heard.Add("title");
        };

        Track("Song");
        Assert.AreSequenceEqual(["before", "title", "after"], heard);
        Assert.AreEqual((uint)1, session.UniqueId);

        heard.Clear();
        Track("Song", number: 2);
        Assert.IsEmpty(heard);
        Assert.AreEqual((uint)1, session.UniqueId);
    }

    [TestMethod]
    public void Metadata_has_what_the_app_gives()
    {
        var session = Session();

        Track("Song", "Artist", "Album", "Band");
        Assert.AreSequenceEqual<string>(["album", "albumArtist", "artist", "title"], session.Metadata.Keys.Order(StringComparer.Ordinal));

        Track("Song", "Artist", "Album", "Band", 3, ["Rock"]);
        Timeline(5, 125);

        Assert.AreEqual("Band", session.Metadata["albumArtist"]);
        Assert.AreEqual(125d, session.Metadata["duration"]);
        Assert.AreEqual(3, session.Metadata["trackNumber"]);
        Assert.AreSequenceEqual(["Rock"], (IReadOnlyList<string>)session.Metadata["genres"]!);
        Assert.AreEqual("Song", session.TrackTitle);
        Assert.AreEqual("Artist", session.TrackArtist);
        Assert.AreEqual("Album", session.TrackAlbum);
        Assert.AreEqual("Band", session.TrackAlbumArtist);
    }

    [TestMethod]
    public void The_playback_commands_reach_the_app_when_it_accepts_them()
    {
        var session = Session();
        _native.ApplyPlayback(new SmtcPlayback(Paused)
        { CanPlay = true, CanPause = true, CanStop = true, CanGoNext = true, CanGoPrevious = true });
        Assert.IsTrue(session.CanTogglePlaying);

        session.Play();
        session.Next();
        session.Previous();
        session.IsPlaying = true;
        _native.ApplyPlayback(new SmtcPlayback(Playing) { CanPlay = true, CanPause = true, CanStop = true });
        session.TogglePlaying();
        session.IsPlaying = false;
        session.Stop();
        session.PlaybackState = MediaPlaybackState.Stopped;

        Assert.AreSequenceEqual(["play", "next", "previous", "play", "pause", "pause", "stop", "stop"], _link.Sent);
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public void Asking_for_the_state_it_is_in_sends_nothing()
    {
        var session = Session();
        _native.ApplyPlayback(new SmtcPlayback(Playing) { CanPlay = true });

        session.Play();

        Assert.IsEmpty(_link.Sent);
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public void A_control_the_app_does_not_allow_does_nothing_and_warns()
    {
        var session = Session();
        _native.ApplyPlayback(new SmtcPlayback(Paused));
        var refused = new Action[]
        {
            session.Play,
            session.Stop,
            session.Next,
            session.Previous,
            () => session.Seek(5),
            () => session.Position = 5,
            () => session.Shuffle = true,
            () => session.LoopState = MediaLoopState.List,
            () => session.Rate = 2,
            session.SwitchTo
        };

        for (var i = 0; i < refused.Length; i++)
        {
            refused[i]();
            Assert.AreEqual(i + 1, _warnings.Count);
        }

        Assert.IsEmpty(_link.Sent);
    }

    [TestMethod]
    public void An_unknown_playback_or_loop_state_is_refused()
    {
        var session = Session();
        _native.ApplyPlayback(new SmtcPlayback(Paused) { LoopSupported = true });

        session.PlaybackState = (MediaPlaybackState)7;
        session.LoopState = (MediaLoopState)7;

        Assert.IsEmpty(_link.Sent);
        Assert.AreEqual(2, _warnings.Count);
        foreach (var w in _warnings)
            Assert.Contains("unknown value 7", w, StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_moment_between_tracks_keeps_the_state_it_had()
    {
        var session = Session();
        _native.ApplyPlayback(new SmtcPlayback(Playing));
        var changes = Watch(session);

        _native.ApplyPlayback(new SmtcPlayback(Changing));

        Assert.AreEqual(MediaPlaybackState.Playing, session.PlaybackState);
        Assert.IsTrue(session.IsPlaying);
        Assert.IsEmpty(changes);
    }

    [TestMethod]
    public void Shuffle_and_repeat_show_only_while_the_app_lets_Windows_change_them()
    {
        var session = Session();
        _native.ApplyPlayback(new SmtcPlayback(Playing) { Shuffle = true, LoopState = MediaLoopState.List });
        Assert.IsFalse(session.Shuffle);
        Assert.AreEqual(MediaLoopState.None, session.LoopState);

        _native.ApplyPlayback(new SmtcPlayback(Playing)
        { Shuffle = true, LoopState = MediaLoopState.List, ShuffleSupported = true, LoopSupported = true });
        Assert.IsTrue(session.Shuffle);
        Assert.AreEqual(MediaLoopState.List, session.LoopState);

        session.Shuffle = false;
        session.LoopState = MediaLoopState.List;
        session.LoopState = MediaLoopState.Track;

        Assert.AreSequenceEqual(["set shuffle", "set loopState"], _link.Sent);
    }

    [TestMethod]
    [DataRow(2, true)]
    [DataRow(0.25, true)]
    [DataRow(4, true)]
    [DataRow(0.2, false)]
    [DataRow(4.5, false)]
    public void A_rate_is_asked_for_between_the_limits(double rate, bool asked)
    {
        var session = Session();
        _native.ApplyPlayback(new SmtcPlayback(Playing) { RateSupported = true });
        Assert.AreEqual(0.25, session.MinRate);
        Assert.AreEqual(4, session.MaxRate);

        session.Rate = rate;

        Assert.AreEqual(asked ? 1 : 0, _link.Sent.Count);
        Assert.AreEqual(asked ? 0 : 1, _warnings.Count);
    }

    [TestMethod]
    public void With_no_speed_control_the_rate_stays_at_one()
    {
        var session = Session();

        Assert.AreEqual(1, session.Rate);
        Assert.AreEqual(1, session.MinRate);
        Assert.AreEqual(1, session.MaxRate);
    }

    [TestMethod]
    public void Position_moves_while_playing_and_holds_while_paused()
    {
        var native = Native(new RecordingLink());
        native.ApplyPlayback(new SmtcPlayback(Playing));
        var now = _loop.Time.GetUtcNow();
        native.ApplyTimeline(TimeSpan.Zero, TimeSpan.FromSeconds(200), TimeSpan.FromSeconds(10), now, now);
        var session = new MediaSession(native, _loop, _log);
        Assert.AreEqual(10, session.Position);

        _loop.Advance(TimeSpan.FromSeconds(5));
        Assert.AreEqual(15, session.Position);

        native.ApplyPlayback(new SmtcPlayback(Paused));
        _loop.Advance(TimeSpan.FromSeconds(5));
        Assert.AreEqual(15, session.Position);
    }

    [TestMethod]
    public void A_stopped_or_silent_session_is_at_position_zero()
    {
        var session = Session();
        Assert.AreEqual(0, session.Position);
        Assert.IsFalse(session.PositionSupported);

        _native.ApplyPlayback(new SmtcPlayback(Stopped));
        Timeline(5, 125);

        Assert.IsTrue(session.PositionSupported);
        Assert.AreEqual(0, session.Position);
    }

    [TestMethod]
    public void Seeking_shows_the_new_position_at_once_and_stays_on_the_track()
    {
        var session = Session();
        _native.ApplyPlayback(new SmtcPlayback(Paused) { CanSeek = true });
        Timeline(5, 125);
        Assert.AreEqual(5, session.Position);
        Assert.AreEqual(125, session.Length);
        var changes = Watch(session);

        session.Seek(10);
        Assert.AreEqual(15, session.Position);
        Assert.AreSequenceEqual([nameof(MediaSession.Position)], changes);

        session.Seek(-100);
        Assert.AreEqual(0, session.Position);

        session.Position = 500;
        Assert.AreEqual(125, session.Position);

        Assert.AreSequenceEqual(["set position", "set position", "set position"], _link.Sent);
    }

    [TestMethod]
    public void A_length_that_matches_the_position_already_shown_is_not_a_length_change()
    {
        var session = Session();
        _native.ApplyPlayback(new SmtcPlayback(Paused));
        Timeline(10, 0);
        Assert.IsFalse(session.LengthSupported);
        Assert.AreEqual(10, session.Length);
        var changes = Watch(session);

        Timeline(10, 10);

        Assert.IsTrue(session.LengthSupported);
        Assert.AreEqual(10, session.Length);
        Assert.Contains(nameof(MediaSession.LengthSupported), changes);
        Assert.DoesNotContain(nameof(MediaSession.Length), changes);
    }

    [TestMethod]
    public void A_real_length_replaces_the_length_the_clock_already_published()
    {
        var session = Session();
        _native.ApplyPlayback(new SmtcPlayback(Playing));
        var now = _loop.Time.GetUtcNow();
        _native.ApplyTimeline(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromSeconds(10), now, now);
        Assert.IsFalse(session.LengthSupported);
        Assert.AreEqual(10, session.Length);

        _loop.Advance(TimeSpan.FromSeconds(10));
        session.RefreshPosition();
        Assert.AreEqual(20, session.Length);
        var changes = Watch(session);

        now = _loop.Time.GetUtcNow();
        _native.ApplyTimeline(TimeSpan.Zero, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), now, now);

        Assert.IsTrue(session.LengthSupported);
        Assert.AreEqual(10, session.Length);
        Assert.Contains(nameof(MediaSession.Length), changes);
    }

    [TestMethod]
    public void A_track_with_no_length_reports_its_position_as_the_length()
    {
        var session = Session();
        _native.ApplyPlayback(new SmtcPlayback(Paused));
        Timeline(5, 0);
        Assert.IsFalse(session.LengthSupported);
        Assert.AreEqual(5, session.Length);
        var changes = Watch(session);

        session.RefreshPosition();

        Assert.AreSequenceEqual([nameof(MediaSession.Position), nameof(MediaSession.Length)], changes);
    }

    [TestMethod]
    public void A_poll_that_repeats_the_last_report_is_not_a_seek()
    {
        var session = Session();
        _native.ApplyPlayback(new SmtcPlayback(Paused));
        Timeline(5, 125);
        var changes = Watch(session);

        Timeline(5, 125);

        Assert.IsEmpty(changes);
    }

    [TestMethod]
    public void A_session_of_a_store_app_or_a_program_can_be_switched_to_and_another_cannot()
    {
        Assert.IsTrue(new MediaSession(Native(new RecordingLink(), "Publisher.App_abc!App"), _loop, _log).CanSwitchTo);
        Assert.IsTrue(new MediaSession(Native(new RecordingLink(), "Spotify.exe"), _loop, _log).CanSwitchTo);
        Assert.IsFalse(Session().CanSwitchTo);
    }

    [TestMethod]
    public void Switching_to_a_program_with_no_window_warns()
    {
        var session = new MediaSession(Native(new RecordingLink(), "no-such-program.exe"), _loop, _log);

        session.SwitchTo();
        Wait.Until(() => _warnings.Count > 0, "the switch warning", _loop.Process);

        Assert.Contains("has no window", _warnings[0], StringComparison.Ordinal);
    }
}
