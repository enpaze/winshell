using Windows.Foundation;
using Windows.Media.Control;
using Winshell.Apps;

namespace Winshell.Media;

internal sealed class SmtcSessions : IMediaBackend
{
    private readonly IEventLoop _loop;
    private readonly Log _log;
    private readonly List<SmtcSession> _sessions = [];
    private readonly AppNames _names;
    private readonly SessionManagerPoll _poll;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    private TypedEventHandler<GlobalSystemMediaTransportControlsSessionManager, SessionsChangedEventArgs>?
        _sessionsChanged;


    internal SmtcSessions(IEventLoop loop, Log log,
        Func<IAsyncOperation<GlobalSystemMediaTransportControlsSessionManager>> request)
    {
        _loop = loop;
        _log = log;
        _poll = new SessionManagerPoll(loop, log, request, manager =>
        {
            Subscribe(manager);
            Read(manager.GetSessions());
        });
        _names = new AppNames(loop, log, AppsService.DisplayName, _poll.Refresh);
        loop.Post(_poll.Refresh);
    }

    public IReadOnlyList<SmtcSession> Sessions => _sessions;

    public event Action<SmtcSession>? SessionAdded;

    public event Action<SmtcSession>? SessionRemoved;

    public void Dispose()
    {
        _poll.Dispose();
        Unsubscribe();
        foreach (var session in _sessions)
            session.Release();
        _sessions.Clear();
    }

    private void Subscribe(GlobalSystemMediaTransportControlsSessionManager manager)
    {
        if (ReferenceEquals(_manager, manager))
            return;
        Unsubscribe();
        _manager = manager;
        _sessionsChanged ??= (_, _) => _loop.Post(_poll.Refresh);
        manager.SessionsChanged += _sessionsChanged;
    }

    private void Unsubscribe()
    {
        if (_manager is not null && _sessionsChanged is not null)
            _manager.SessionsChanged -= _sessionsChanged;
        _manager = null;
    }

    private void Read(IReadOnlyList<GlobalSystemMediaTransportControlsSession> current)
    {
        var incoming = new List<GlobalSystemMediaTransportControlsSession>();
        var ids = new List<string>();
        foreach (var session in current)
            if (SmtcSession.IdOf(session) is { } id)
            {
                incoming.Add(session);
                ids.Add(id);
            }

        var held = _sessions.ToList();
        var pair = Pair(held.Select(s => s.AppId).ToList(), ids);
        var kept = new bool[held.Count];
        for (var i = 0; i < incoming.Count; i++)
        {
            var existing = pair[i];
            if (existing >= 0)
            {
                kept[existing] = true;
                held[existing].Retarget(incoming[i]);
                held[existing].Rename(_names.Get(ids[i]));
                continue;
            }

            var session = new SmtcSession(_loop, _log, ids[i], _names.Get(ids[i]),
                new SmtcSessionLink(_loop, _log, incoming[i]));
            _sessions.Add(session);
            SessionAdded?.Invoke(session);
            session.Start();
        }

        for (var i = 0; i < held.Count; i++)
        {
            if (kept[i])
                continue;
            _sessions.Remove(held[i]);
            held[i].Release();
            SessionRemoved?.Invoke(held[i]);
        }
    }

    internal static int[] Pair(IReadOnlyList<string> heldAppIds, IReadOnlyList<string> incomingAppIds)
    {
        var pair = new int[incomingAppIds.Count];
        var used = new bool[heldAppIds.Count];
        for (var i = 0; i < incomingAppIds.Count; i++)
        {
            pair[i] = -1;
            for (var h = 0; h < heldAppIds.Count; h++)
            {
                if (used[h] || heldAppIds[h] != incomingAppIds[i])
                    continue;
                used[h] = true;
                pair[i] = h;
                break;
            }
        }

        return pair;
    }
}
