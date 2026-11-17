using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Winshell.Media;

internal sealed class SmtcSession
{
    private readonly ISmtcLink _link;
    private readonly SessionArt _art;
    private readonly TimeProvider _time;
    private SmtcPlayback _playback = new(GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed);
    private bool _released;
    private DateTimeOffset _positionTime;
    private double _position;

    public SmtcSession(IEventLoop loop, Log log, string appId, string appName, ISmtcLink link)
    {
        AppId = appId;
        AppName = appName;
        _time = loop.Time;
        _positionTime = _time.GetUtcNow();
        _art = new SessionArt(loop, reason => log.Warn("media", $"Cannot read art from {AppName}: {reason}"),
            () => Changed?.Invoke());
        _link = link;
    }

    internal static string? IdOf(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            return session.SourceAppUserModelId ?? "";
        }
        catch (COMException)
        {
            return null;
        }
    }

    public string AppId { get; }

    public string AppName { get; private set; }

    internal void Rename(string name)
    {
        if (_released || name.Length == 0 || name == AppName)
            return;
        AppName = name;
        Changed?.Invoke();
    }

    public string Title { get; private set; } = "";

    public string Artist { get; private set; } = "";

    public string Album { get; private set; } = "";

    public string AlbumArtist { get; private set; } = "";

    public IReadOnlyList<string> Genres { get; private set; } = [];

    public int TrackNumber { get; private set; }

    public string ArtUrl => _art.Url;

    public uint UniqueId { get; private set; }

    internal SmtcPlayback Playback => _playback;

    public MediaPlaybackState PlaybackState { get; private set; }

    public double Length { get; private set; }

    public bool LengthSupported => Length > 0;

    public bool PositionSupported { get; private set; }

    private TimeSpan StartTime { get; set; }

    public event Action? Changed;

    public event Action? Seeked;

    public double PositionNow()
    {
        if (!PositionSupported || PlaybackState == MediaPlaybackState.Stopped)
            return 0;
        var position = _position;
        if (PlaybackState == MediaPlaybackState.Playing)
            position += (_time.GetUtcNow() - _positionTime).TotalSeconds * _playback.Rate;
        return Length > 0 ? Math.Clamp(position, 0, Length) : Math.Max(0, position);
    }

    public void Start()
    {
        _link.Start(this);
    }

    public void Retarget(GlobalSystemMediaTransportControlsSession session)
    {
        _link.Retarget(session);
    }

    public void Release()
    {
        _released = true;
        _link.Release();
        _art.Release();
    }

    public void Play()
    {
        Send("play", s => s.TryPlayAsync());
    }

    public void Pause()
    {
        Send("pause", s => s.TryPauseAsync());
    }

    public void Stop()
    {
        Send("stop", s => s.TryStopAsync());
    }

    public void Next()
    {
        Send("next", s => s.TrySkipNextAsync());
    }

    public void Previous()
    {
        Send("previous", s => s.TrySkipPreviousAsync());
    }

    public void SetShuffle(bool shuffle)
    {
        Send("set shuffle", s => s.TryChangeShuffleActiveAsync(shuffle));
    }

    public void SetLoopState(MediaLoopState state)
    {
        Send("set loopState", s => s.TryChangeAutoRepeatModeAsync(SmtcPlayback.ModeOf(state)));
    }

    public void SetRate(double rate)
    {
        Send("set rate", s => s.TryChangePlaybackRateAsync(rate));
    }

    public void SetPosition(double seconds)
    {
        seconds = Length > 0 ? Math.Clamp(seconds, 0, Length) : Math.Max(0, seconds);
        Send("set position", s => s.TryChangePlaybackPositionAsync((StartTime + TimeSpan.FromSeconds(seconds)).Ticks));
        _position = seconds;
        _positionTime = _time.GetUtcNow();
        Seeked?.Invoke();
    }

    private void Send(string what, Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> command)
    {
        _link.Send(what, command);
    }

    internal void ApplyTrack(string title, string artist, string album, string albumArtist, int trackNumber,
        IReadOnlyList<string>? genres,
        IRandomAccessStreamReference? thumbnail)
    {
        if (title == Title && artist == Artist && album == Album && albumArtist == AlbumArtist
            && TrackNumber == trackNumber && SameGenres(genres))
        {
            if (ArtUrl.Length == 0 && thumbnail is not null && !_art.Loading)
                _art.Load(thumbnail);
            return;
        }

        var newTrack = title != Title || artist != Artist || album != Album;
        if (newTrack)
            UniqueId++;
        Title = title;
        Artist = artist;
        Album = album;
        AlbumArtist = albumArtist;
        Genres = genres?.ToArray() ?? [];
        TrackNumber = trackNumber;
        Changed?.Invoke();
        if (newTrack)
            Seeked?.Invoke();
        _art.Load(thumbnail);
    }

    internal void ApplyPlayback(SmtcPlayback info)
    {
        var state = info.Status switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => MediaPlaybackState.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => MediaPlaybackState.Paused,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing => PlaybackState,
            _ => MediaPlaybackState.Stopped
        };
        var seeked = false;
        if (state != PlaybackState || info.Rate != _playback.Rate)
        {
            _position = PositionNow();
            _positionTime = _time.GetUtcNow();
            seeked = true;
        }

        PlaybackState = state;
        _playback = info with
        {
            Shuffle = info.ShuffleSupported && info.Shuffle,
            LoopState = info.LoopSupported ? info.LoopState : MediaLoopState.None
        };
        Changed?.Invoke();
        if (seeked)
            Seeked?.Invoke();
    }

    internal void ApplyTimeline(TimeSpan start, TimeSpan end, TimeSpan reported, DateTimeOffset updated,
        DateTimeOffset received)
    {
        var length = Math.Max(0, (end - start).TotalSeconds);
        var supported = length > 0 || reported > TimeSpan.Zero;
        var position = (reported - start).TotalSeconds;
        if (StartTime == start && Length == length && PositionSupported == supported
            && Math.Abs(_position - position) < 0.001)
            return;
        StartTime = start;
        PositionSupported = supported;
        Length = length;
        var time = updated.Year > 2000 && updated <= received && received - updated < TimeSpan.FromHours(1)
            ? updated
            : received;
        _position = position;
        _positionTime = time;
        Changed?.Invoke();
        Seeked?.Invoke();
    }

    private bool SameGenres(IReadOnlyList<string>? genres)
    {
        var count = genres?.Count ?? 0;
        if (count != Genres.Count)
            return false;
        for (var i = 0; i < count; i++)
            if (genres![i] != Genres[i])
                return false;
        return true;
    }
}
