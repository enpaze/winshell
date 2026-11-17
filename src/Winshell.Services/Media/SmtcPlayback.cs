using Windows.Media;
using Windows.Media.Control;

namespace Winshell.Media;

internal sealed record SmtcPlayback(GlobalSystemMediaTransportControlsSessionPlaybackStatus Status)
{
    public double Rate { get; init; } = 1;

    public bool CanPlay { get; init; }

    public bool CanPause { get; init; }

    public bool CanStop { get; init; }

    public bool CanGoNext { get; init; }

    public bool CanGoPrevious { get; init; }

    public bool CanSeek { get; init; }

    public bool ShuffleSupported { get; init; }

    public bool LoopSupported { get; init; }

    public bool RateSupported { get; init; }

    public bool Shuffle { get; init; }

    public MediaLoopState LoopState { get; init; }

    public static SmtcPlayback From(GlobalSystemMediaTransportControlsSessionPlaybackInfo info)
    {
        var controls = info.Controls;
        return new(info.PlaybackStatus)
        {
            Rate = info.PlaybackRate ?? 1,
            CanPlay = controls.IsPlayEnabled,
            CanPause = controls.IsPauseEnabled,
            CanStop = controls.IsStopEnabled,
            CanGoNext = controls.IsNextEnabled,
            CanGoPrevious = controls.IsPreviousEnabled,
            CanSeek = controls.IsPlaybackPositionEnabled,
            ShuffleSupported = controls.IsShuffleEnabled,
            LoopSupported = controls.IsRepeatEnabled,
            RateSupported = controls.IsPlaybackRateEnabled,
            Shuffle = info.IsShuffleActive ?? false,
            LoopState = LoopOf(info.AutoRepeatMode)
        };
    }

    public static MediaLoopState LoopOf(MediaPlaybackAutoRepeatMode? mode)
    {
        return mode switch
        {
            MediaPlaybackAutoRepeatMode.Track => MediaLoopState.Track,
            MediaPlaybackAutoRepeatMode.List => MediaLoopState.List,
            _ => MediaLoopState.None
        };
    }

    public static MediaPlaybackAutoRepeatMode ModeOf(MediaLoopState state)
    {
        return state switch
        {
            MediaLoopState.Track => MediaPlaybackAutoRepeatMode.Track,
            MediaLoopState.List => MediaPlaybackAutoRepeatMode.List,
            _ => MediaPlaybackAutoRepeatMode.None
        };
    }
}
