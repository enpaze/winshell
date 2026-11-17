using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Media.Control;

namespace Winshell.Media;

internal sealed class SmtcSessionLink(
    IEventLoop loop,
    Log log,
    GlobalSystemMediaTransportControlsSession session) : ISmtcLink
{
    private readonly NewestRead _media = new();
    private SmtcSession? _target;
    private bool _released;

    private TypedEventHandler<GlobalSystemMediaTransportControlsSession, MediaPropertiesChangedEventArgs>?
        _mediaChanged;

    private TypedEventHandler<GlobalSystemMediaTransportControlsSession, PlaybackInfoChangedEventArgs>?
        _playbackChanged;

    private TypedEventHandler<GlobalSystemMediaTransportControlsSession, TimelinePropertiesChangedEventArgs>?
        _timelineChanged;

    private GlobalSystemMediaTransportControlsSession _session = session;

    public void Start(SmtcSession target)
    {
        _target = target;
        Hook();
        OnPlaybackInfoChanged(_session, null);
        OnTimelinePropertiesChanged(_session, null);
        OnMediaPropertiesChanged(_session, null);
    }

    public void Retarget(GlobalSystemMediaTransportControlsSession session)
    {
        if (ReferenceEquals(_session, session))
            return;
        Unhook();
        _session = session;
        Hook();
        OnPlaybackInfoChanged(_session, null);
        OnTimelinePropertiesChanged(_session, null);
        if (!_media.Running)
            OnMediaPropertiesChanged(_session, null);
    }

    public void Release()
    {
        _released = true;
        Unhook();
    }

    public void Send(string what, Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> command)
    {
        _ = Await();

        async Task Await()
        {
            try
            {
                if (!await command(_session).AsTask().ConfigureAwait(false))
                    loop.Post(() => log.Warn("media", $"{_target?.AppName} did not accept {what}"));
            }
#pragma warning disable CA1031
            catch (Exception e)
#pragma warning restore CA1031
            {
                loop.Post(() => log.Warn("media", $"Cannot {what} on {_target?.AppName}: {e.Message}"));
            }
        }
    }

    private void Hook()
    {
        _mediaChanged ??= OnMediaPropertiesChanged;
        _playbackChanged ??= OnPlaybackInfoChanged;
        _timelineChanged ??= OnTimelinePropertiesChanged;
        _session.MediaPropertiesChanged += _mediaChanged;
        _session.PlaybackInfoChanged += _playbackChanged;
        _session.TimelinePropertiesChanged += _timelineChanged;
    }

    private void Unhook()
    {
        if (_mediaChanged is not null)
            _session.MediaPropertiesChanged -= _mediaChanged;
        if (_playbackChanged is not null)
            _session.PlaybackInfoChanged -= _playbackChanged;
        if (_timelineChanged is not null)
            _session.TimelinePropertiesChanged -= _timelineChanged;
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs? args)
    {
        var request = _media.Start();
        _ = Read();

        async Task Read()
        {
            try
            {
                var properties = await sender.TryGetMediaPropertiesAsync().AsTask().ConfigureAwait(false);
                if (properties is not null)
                    loop.Post(() => ApplyMedia(properties, request));
            }
#pragma warning disable CA1031
            catch (Exception e)
#pragma warning restore CA1031
            {
                loop.Post(() =>
                {
                    if (e is not COMException)
                        log.Warn("media", $"Cannot read {_target?.AppName}'s track: {e.Message}");
                    if (!_released)
                        _media.End(request);
                });
            }
        }
    }

    private void ApplyMedia(GlobalSystemMediaTransportControlsSessionMediaProperties properties, int request)
    {
        if (_released || !_media.End(request))
            return;
        _target?.ApplyTrack(properties.Title ?? "", properties.Artist ?? "", properties.AlbumTitle ?? "",
            properties.AlbumArtist ?? "",
            properties.TrackNumber, properties.Genres, properties.Thumbnail);
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs? args)
    {
        try
        {
            var info = sender.GetPlaybackInfo();
            loop.Post(() => ApplyPlayback(info));
        }
        catch (COMException)
        {
        }
    }

    private void ApplyPlayback(GlobalSystemMediaTransportControlsSessionPlaybackInfo info)
    {
        if (_released)
            return;
        _target?.ApplyPlayback(SmtcPlayback.From(info));
    }

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender,
        TimelinePropertiesChangedEventArgs? args)
    {
        try
        {
            var timeline = sender.GetTimelineProperties();
            var received = loop.Time.GetUtcNow();
            loop.Post(() => ApplyTimeline(timeline, received));
        }
        catch (COMException)
        {
        }
    }

    private void ApplyTimeline(GlobalSystemMediaTransportControlsSessionTimelineProperties timeline,
        DateTimeOffset received)
    {
        if (!_released)
            _target?.ApplyTimeline(timeline.StartTime, timeline.EndTime, timeline.Position, timeline.LastUpdatedTime,
                received);
    }
}
