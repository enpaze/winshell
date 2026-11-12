using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Winshell.Apps;
using Winshell.Native;

namespace Winshell.Media;

/// <summary>One app's entry in the Windows media controls.</summary>
public sealed class MediaSession : ObservableObject
{
    // Every public property, read through its getter, so a new one is raised without a second list. Position moves
    // with the clock between reports, so only Seeked raises it. Length does too while the app reports none, and
    // that pass records the value it published.
    private static readonly PropertyInfo[] Reported = typeof(MediaSession)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(static property => property.Name != nameof(Position))
        .ToArray();

    private static readonly int LengthReport = Array.FindIndex(Reported, static property => property.Name == nameof(Length));

    private readonly SmtcSession _session;
    private readonly IEventLoop _loop;
    private readonly Log _log;
    private object?[] _reported;
    private uint _track;

    internal MediaSession(SmtcSession session, IEventLoop loop, Log log)
    {
        _session = session;
        _loop = loop;
        _log = log;
        _reported = Snapshot();
        _track = session.UniqueId;
        session.Changed += OnChanged;
        session.Seeked += RefreshPosition;
    }

    internal SmtcSession Native => _session;

    /// <summary>Whether the app accepts <see cref="Play"/>.</summary>
    public bool CanPlay => Read(_session).Playback.CanPlay;

    /// <summary>Whether the app accepts <see cref="Pause"/>.</summary>
    public bool CanPause => Read(_session).Playback.CanPause;

    /// <summary>Whether the app accepts <see cref="Stop"/>.</summary>
    public bool CanStop => Read(_session).Playback.CanStop;

    /// <summary>
    /// Whether <see cref="TogglePlaying"/> can work: <see cref="CanPause"/> while playing, <see cref="CanPlay"/>
    /// otherwise.
    /// </summary>
    public bool CanTogglePlaying => Read(_session).PlaybackState == MediaPlaybackState.Playing ? _session.Playback.CanPause : _session.Playback.CanPlay;

    /// <summary>Whether the app accepts a new <see cref="Position"/> and <see cref="Seek"/>.</summary>
    public bool CanSeek => Read(_session).Playback.CanSeek;

    /// <summary>Whether the app accepts <see cref="Next"/>.</summary>
    public bool CanGoNext => Read(_session).Playback.CanGoNext;

    /// <summary>Whether the app accepts <see cref="Previous"/>.</summary>
    public bool CanGoPrevious => Read(_session).Playback.CanGoPrevious;

    /// <summary>True when the session accepts at least one playback command.</summary>
    public bool CanControl => Read(_session).Playback.CanPlay || _session.Playback.CanPause || _session.Playback.CanStop || _session.Playback.CanSeek ||
                              _session.Playback.CanGoNext || _session.Playback.CanGoPrevious;

    /// <summary>
    /// True when <see cref="SwitchTo"/> knows how to find the app: a Store app, or a program whose
    /// <see cref="AppId"/> is its file name.
    /// </summary>
    public bool CanSwitchTo => AppIdentity.IsPackaged(Read(_session).AppId) ||
                               AppIdentity.ProgramName(_session.AppId) is not null;

    /// <summary>The name Windows shows for the app.</summary>
    public string AppName => Read(_session).AppName;

    /// <summary>The app's AppUserModelID, such as <c>Spotify.exe</c> or a Store app's id.</summary>
    public string AppId => Read(_session).AppId;

    /// <summary>
    /// Where playback is in the track, in seconds. It counts forward while playing, but a change is only raised
    /// when the app reports a position, seeks, changes track, or pauses or plays. To show it moving, call
    /// <see cref="RefreshPosition"/> from a timer.
    /// </summary>
    public double Position
    {
        get => Read(_session).PositionNow();
        set
        {
            if (!_session.PositionSupported)
            {
                Warn($"Cannot set the position of {AppName} because position is not supported.");
                return;
            }

            if (!_session.Playback.CanSeek)
            {
                Warn($"Cannot set the position of {AppName} because CanSeek is false.");
                return;
            }

            _session.SetPosition(value);
        }
    }

    /// <summary>Whether the app reports where playback is. When false, <see cref="Position"/> is 0.</summary>
    public bool PositionSupported => Read(_session).PositionSupported;

    /// <summary>The track's length in seconds. When the app does not report one, it is the same as <see cref="Position"/>.</summary>
    public double Length => Read(_session).LengthSupported ? _session.Length : _session.PositionNow();

    /// <summary>Whether the app reports the track's length.</summary>
    public bool LengthSupported => Read(_session).LengthSupported;

    /// <summary>
    /// The track as Windows reports it: <c>title</c>, <c>artist</c>, <c>album</c> and <c>albumArtist</c>, and when the
    /// app gives them, <c>artUrl</c>, <c>duration</c> in seconds, <c>trackNumber</c>, and <c>genres</c>, a list.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Metadata
    {
        get
        {
            var metadata = new Dictionary<string, object?>
            {
                ["title"] = TrackTitle,
                ["artist"] = TrackArtist,
                ["album"] = TrackAlbum,
                ["albumArtist"] = TrackAlbumArtist
            };
            if (TrackArtUrl.Length > 0)
                metadata["artUrl"] = TrackArtUrl;
            if (LengthSupported)
                metadata["duration"] = Length;
            if (_session.TrackNumber > 0)
                metadata["trackNumber"] = _session.TrackNumber;
            if (_session.Genres.Count > 0)
                metadata["genres"] = _session.Genres;
            return metadata;
        }
    }

    /// <summary>A number that goes up by one each time the title, artist or album changes.</summary>
    public uint UniqueId => Read(_session).UniqueId;

    /// <summary>The track's title. Empty when the app gives none.</summary>
    public string TrackTitle => Read(_session).Title;

    /// <summary>The track's artist. Empty when the app gives none.</summary>
    public string TrackArtist => Read(_session).Artist;

    /// <summary>The track's album. Empty when the app gives none.</summary>
    public string TrackAlbum => Read(_session).Album;

    /// <summary>The artist of the track's album. Empty when the app gives none.</summary>
    public string TrackAlbumArtist => Read(_session).AlbumArtist;

    /// <summary>A <c>file:///</c> url of the art Windows gives, written to the temp folder.</summary>
    public string TrackArtUrl => Read(_session).ArtUrl;

    /// <summary>Setting it calls <see cref="Play"/>, <see cref="Pause"/> or <see cref="Stop"/>.</summary>
    public MediaPlaybackState PlaybackState
    {
        get => Read(_session).PlaybackState;
        set
        {
            if (value == PlaybackState)
                return;
            switch (value)
            {
                case MediaPlaybackState.Stopped:
                    if (_session.Playback.CanStop)
                        _session.Stop();
                    else
                        Warn($"Cannot set the playback state of {AppName} to Stopped because CanStop is false.");
                    break;
                case MediaPlaybackState.Playing:
                    if (_session.Playback.CanPlay)
                        _session.Play();
                    else
                        Warn($"Cannot set the playback state of {AppName} to Playing because CanPlay is false.");
                    break;
                case MediaPlaybackState.Paused:
                    if (_session.Playback.CanPause)
                        _session.Pause();
                    else
                        Warn($"Cannot set the playback state of {AppName} to Paused because CanPause is false.");
                    break;
                default:
                    Warn($"Cannot set the playback state of {AppName} to unknown value {(int)value}.");
                    break;
            }
        }
    }

    /// <summary>True while playing. Setting it plays or pauses.</summary>
    public bool IsPlaying
    {
        get => Read(_session).PlaybackState == MediaPlaybackState.Playing;
        set
        {
            if (value != IsPlaying)
                TogglePlaying();
        }
    }

    /// <summary>
    /// What the app repeats. <see cref="MediaLoopState.None"/> when <see cref="LoopSupported"/> is false, and then
    /// setting it does nothing.
    /// </summary>
    public MediaLoopState LoopState
    {
        get => Read(_session).Playback.LoopState;
        set
        {
            if (!_session.Playback.LoopSupported)
            {
                Warn($"Cannot set the loop state of {AppName} because loop state is not supported.");
                return;
            }

            if (value == LoopState)
                return;
            if (!Enum.IsDefined(value))
            {
                Warn($"Cannot set the loop state of {AppName} to unknown value {(int)value}.");
                return;
            }

            _session.SetLoopState(value);
        }
    }

    /// <summary>Whether the app accepts a new <see cref="LoopState"/>.</summary>
    public bool LoopSupported => Read(_session).Playback.LoopSupported;

    /// <summary>The playback speed; 1 is normal.</summary>
    public double Rate
    {
        get => Read(_session).Playback.Rate;
        set
        {
            if (value == _session.Playback.Rate)
                return;
            if (value < MinRate || value > MaxRate)
            {
                Warn(
                    $"Cannot set the rate of {AppName} to {value.ToString(CultureInfo.InvariantCulture)}, outside of {MinRate.ToString(CultureInfo.InvariantCulture)} to {MaxRate.ToString(CultureInfo.InvariantCulture)}.");
                return;
            }

            _session.SetRate(value);
        }
    }

    /// <summary>Windows does not say which rates an app accepts; these are the common ones when it takes any.</summary>
    public double MinRate => Read(_session).Playback.RateSupported ? 0.25 : 1;

    /// <summary>The fastest <see cref="Rate"/> to set: 4 when the app accepts rate changes, otherwise 1.</summary>
    public double MaxRate => Read(_session).Playback.RateSupported ? 4 : 1;

    /// <summary>
    /// Whether the app plays in shuffled order. False when <see cref="ShuffleSupported"/> is false, and then setting
    /// it does nothing.
    /// </summary>
    public bool Shuffle
    {
        get => Read(_session).Playback.Shuffle;
        set
        {
            if (!_session.Playback.ShuffleSupported)
            {
                Warn($"Cannot set shuffle for {AppName} because shuffle is not supported.");
                return;
            }

            _session.SetShuffle(value);
        }
    }

    /// <summary>Whether the app accepts a new <see cref="Shuffle"/>.</summary>
    public bool ShuffleSupported => Read(_session).Playback.ShuffleSupported;

    /// <summary>The track changed. It comes before the track's properties are updated.</summary>
    public event Action? TrackChanged;

    /// <summary>The track changed, after the track's properties are updated.</summary>
    public event Action? PostTrackChanged;

    /// <summary>Asks the app to play. Logs a warning when <see cref="CanPlay"/> is false.</summary>
    public void Play()
    {
        PlaybackState = MediaPlaybackState.Playing;
    }

    /// <summary>Asks the app to pause. Logs a warning when <see cref="CanPause"/> is false.</summary>
    public void Pause()
    {
        PlaybackState = MediaPlaybackState.Paused;
    }

    /// <summary>Asks the app to stop. Logs a warning when <see cref="CanStop"/> is false.</summary>
    public void Stop()
    {
        PlaybackState = MediaPlaybackState.Stopped;
    }

    /// <summary>Calls <see cref="Pause"/> while playing, and <see cref="Play"/> otherwise.</summary>
    public void TogglePlaying()
    {
        if (IsPlaying)
            Pause();
        else
            Play();
    }

    /// <summary>Asks the app to skip to the next track. Logs a warning when <see cref="CanGoNext"/> is false.</summary>
    public void Next()
    {
        if (!_session.Playback.CanGoNext)
        {
            Warn($"Cannot call Next() on {AppName} because CanGoNext is false.");
            return;
        }

        _session.Next();
    }

    /// <summary>Asks the app to go back a track. Logs a warning when <see cref="CanGoPrevious"/> is false.</summary>
    public void Previous()
    {
        if (!_session.Playback.CanGoPrevious)
        {
            Warn($"Cannot call Previous() on {AppName} because CanGoPrevious is false.");
            return;
        }

        _session.Previous();
    }

    /// <summary>Windows has no relative seek, so this sets the position to the current one plus the offset.</summary>
    public void Seek(double offset)
    {
        if (!_session.Playback.CanSeek)
        {
            Warn($"Cannot call Seek() on {AppName} because CanSeek is false.");
            return;
        }

        _session.SetPosition(_session.PositionNow() + offset);
    }

    /// <summary>
    /// Brings the app's window forward. A program's window is looked for off the UI thread, so it comes forward
    /// a moment later. A warning is logged when the program has no window or the app cannot be opened.
    /// </summary>
    public void SwitchTo()
    {
        if (AppIdentity.IsPackaged(AppId))
        {
            // A Store app is single-instance: starting it again brings its window forward.
            if (AppIdentity.OpenAppsFolder(AppId) is { } failed)
                CannotSwitch(failed);
        }
        else if (AppIdentity.ProgramName(AppId) is { } program)
        {
            _ = Task.Run(() =>
            {
                // An exception in this task would be lost, so it is reported on the loop.
                nint window;
                try
                {
                    window = MainWindow(program);
                }
#pragma warning disable CA1031
                catch (Exception e)
#pragma warning restore CA1031
                {
                    var message = e.Message;
                    _loop.Post(() => CannotSwitch(message));
                    return;
                }

                _loop.Post(() => Raise(window));
            });
        }
        else
        {
            Warn($"Cannot call SwitchTo() on {AppName} because CanSwitchTo is false.");
        }
    }

    /// <summary>Raises a change of <see cref="Position"/>, so a binding reads where playback has got to.</summary>
    public void RefreshPosition()
    {
        Changed(nameof(Position));
        if (_session.LengthSupported)
            return;
        Changed(nameof(Length));
        _reported[LengthReport] = Length;
    }

    internal void Detach()
    {
        _session.Changed -= OnChanged;
        _session.Seeked -= RefreshPosition;
    }

    private static nint MainWindow(string program)
    {
        foreach (var process in Process.GetProcessesByName(program))
            using (process)
            {
                if (process.MainWindowHandle != 0)
                    return process.MainWindowHandle;
            }

        return 0;
    }

    private void Raise(nint window)
    {
        if (window == 0)
        {
            Warn($"Cannot call SwitchTo() on {AppName} because it has no window.");
            return;
        }

        if (PInvoke.IsIconic((HWND)window))
            TopLevelWindows.Restore(window);
        Focus.Take(window);
    }

    private void CannotSwitch(string reason)
    {
        Warn($"Cannot call SwitchTo() on {AppName}: {reason}");
    }

    private void Warn(string message)
    {
        _log.Warn("media", message);
    }

    private object?[] Snapshot()
    {
        var values = new object?[Reported.Length];
        for (var i = 0; i < values.Length; i++)
            values[i] = Reported[i].GetValue(this);
        return values;
    }

    private void OnChanged()
    {
        var was = _reported;
        _reported = Snapshot();
        // Length is the moving position while the app reports none. Seeked publishes that and records it.
        if (!LengthSupported)
            _reported[LengthReport] = was[LengthReport];
        var newTrack = _track != UniqueId;
        _track = UniqueId;
        if (newTrack)
            TrackChanged?.Invoke();
        for (var i = 0; i < Reported.Length; i++)
            Changed(!Same(was[i], _reported[i]), Reported[i].Name);

        if (newTrack)
            PostTrackChanged?.Invoke();
    }

    private static bool Same(object? a, object? b)
    {
        return (a, b) switch
        {
            (IReadOnlyDictionary<string, object?> x, IReadOnlyDictionary<string, object?> y) =>
                x.Count == y.Count && x.All(e => y.TryGetValue(e.Key, out var v) && Same(e.Value, v)),
            (IReadOnlyList<string> x, IReadOnlyList<string> y) => x.SequenceEqual(y),
            _ => Equals(a, b)
        };
    }
}
