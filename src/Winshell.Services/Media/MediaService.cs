using Windows.Media.Control;

namespace Winshell.Media;

public sealed class MediaService : ObservableObject, IDisposable
{
    private readonly IMediaBackend _sessions;
    private readonly IEventLoop _loop;
    private readonly Log _log;

    internal MediaService(IEventLoop loop, Log log, IMediaBackend? backend = null)
    {
        _loop = loop;
        _log = log;
        _sessions = backend ??
                    new SmtcSessions(loop, log, GlobalSystemMediaTransportControlsSessionManager.RequestAsync);
        Refresh(null);
        _sessions.SessionAdded += Refresh;
        _sessions.SessionRemoved += Refresh;
        if (backend is null)
            TempImageStore.Shared.CleanOldOnce(failure => log.Warn("images", failure));
    }

    public ObservableList<MediaSession> Sessions { get => Read(field); } = [];

    private void Refresh(SmtcSession? _)
    {
        Sessions.Sync(_sessions.Sessions, s => s.Native, n => n, n => new MediaSession(n, _loop, _log),
            static (_, _) => { }, s => s.Detach());
    }

    void IDisposable.Dispose()
    {
        _sessions.SessionAdded -= Refresh;
        _sessions.SessionRemoved -= Refresh;
        foreach (var session in Sessions)
            session.Detach();
        _sessions.Dispose();
    }
}
