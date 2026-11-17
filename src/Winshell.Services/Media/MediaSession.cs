using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Winshell.Apps;
using Winshell.Native;

namespace Winshell.Media;

public sealed class MediaSession : ObservableObject
{
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

    public bool CanPlay => Read(_session).Playback.CanPlay;

    public bool CanPause => Read(_session).Playback.CanPause;

    public bool CanStop => Read(_session).Playback.CanStop;

    public bool CanTogglePlaying => Read(_session).PlaybackState == MediaPlaybackState.Playing ? _session.Playback.CanPause : _session.Playback.CanPlay;

    public bool CanSeek => Read(_session).Playback.CanSeek;

    public bool CanGoNext => Read(_session).Playback.CanGoNext;

    public bool CanGoPrevious => Read(_session).Playback.CanGoPrevious;

    public bool CanControl => Read(_session).Playback.CanPlay || _session.Playback.CanPause || _session.Playback.CanStop || _session.Playback.CanSeek ||
                              _session.Playback.CanGoNext || _session.Playback.CanGoPrevious;

    public bool CanSwitchTo => AppIdentity.IsPackaged(Read(_session).AppId) ||
                               AppIdentity.ProgramName(_session.AppId) is not null;

    public string AppName => Read(_session).AppName;

    public string AppId => Read(_session).AppId;

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

    public bool PositionSupported => Read(_session).PositionSupported;

    public double Length => Read(_session).LengthSupported ? _session.Length : _session.PositionNow();

    public bool LengthSupported => Read(_session).LengthSupported;

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

    public uint UniqueId => Read(_session).UniqueId;

    public string TrackTitle => Read(_session).Title;

    public string TrackArtist => Read(_session).Artist;

    public string TrackAlbum => Read(_session).Album;

    public string TrackAlbumArtist => Read(_session).AlbumArtist;

    public string TrackArtUrl => Read(_session).ArtUrl;

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

    public bool IsPlaying
    {
        get => Read(_session).PlaybackState == MediaPlaybackState.Playing;
        set
        {
            if (value != IsPlaying)
                TogglePlaying();
        }
    }

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

    public bool LoopSupported => Read(_session).Playback.LoopSupported;

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

    public double MinRate => Read(_session).Playback.RateSupported ? 0.25 : 1;

    public double MaxRate => Read(_session).Playback.RateSupported ? 4 : 1;

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

    public bool ShuffleSupported => Read(_session).Playback.ShuffleSupported;

    public event Action? TrackChanged;

    public event Action? PostTrackChanged;

    public void Play()
    {
        PlaybackState = MediaPlaybackState.Playing;
    }

    public void Pause()
    {
        PlaybackState = MediaPlaybackState.Paused;
    }

    public void Stop()
    {
        PlaybackState = MediaPlaybackState.Stopped;
    }

    public void TogglePlaying()
    {
        if (IsPlaying)
            Pause();
        else
            Play();
    }

    public void Next()
    {
        if (!_session.Playback.CanGoNext)
        {
            Warn($"Cannot call Next() on {AppName} because CanGoNext is false.");
            return;
        }

        _session.Next();
    }

    public void Previous()
    {
        if (!_session.Playback.CanGoPrevious)
        {
            Warn($"Cannot call Previous() on {AppName} because CanGoPrevious is false.");
            return;
        }

        _session.Previous();
    }

    public void Seek(double offset)
    {
        if (!_session.Playback.CanSeek)
        {
            Warn($"Cannot call Seek() on {AppName} because CanSeek is false.");
            return;
        }

        _session.SetPosition(_session.PositionNow() + offset);
    }

    public void SwitchTo()
    {
        if (AppIdentity.IsPackaged(AppId))
        {
            if (AppIdentity.OpenAppsFolder(AppId) is { } failed)
                CannotSwitch(failed);
        }
        else if (AppIdentity.ProgramName(AppId) is { } program)
        {
            _ = Task.Run(() =>
            {
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
