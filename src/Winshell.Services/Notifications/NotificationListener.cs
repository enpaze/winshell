using System.Globalization;

namespace Winshell.Notifications;

public sealed class NotificationListener : ObservableObject, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    private readonly IEventLoop _loop;
    private readonly Log _log;
    private readonly INotificationBackend _backend;
    private readonly ToastChanges _changes;
    private readonly NotificationAppIcons _icons;
    private readonly CancellationTokenSource _stop = new();
    private readonly StateReader<NotificationSnapshot> _reader;
    private IDisposable _poll;

    private Dictionary<(uint Id, DateTimeOffset Created), IncomingToast> _read = [];

    private IReadOnlyList<IncomingToast> _toasts = [];
    private Action? _unwatch;

    private NotificationAccessStatus _accessStatus;
    private NotificationState _focusState;
    private NotificationMode _notificationMode = NotificationMode.Unknown;
    private string _error = "";
    private bool _requesting;
    private bool _disposed;

    internal NotificationListener(IEventLoop loop, Log log, INotificationBackend? backend = null)
    {
        _loop = loop;
        _log = log;
        _backend = backend ?? new NotificationBackend();
        _changes = new ToastChanges(_backend.ToastStamp, loop.Time);
        _icons = new NotificationAppIcons(loop, log, _stop.Token);
        _reader = new StateReader<NotificationSnapshot>(c => OwnThread.Run(() => Read(c)), Apply, loop,
            new FailureWarning(log, "notifications"));
        _poll = SchedulePoll();
        _reader.Request();
        if (backend is null)
            TempImageStore.Shared.CleanOldOnce(failure => log.Warn("images", failure));
    }

    public event Action<Notification>? NotificationAdded;

    public event Action<string>? OperationFailed;

    public ObservableList<Notification> Notifications { get => Read(field); } = [];

    public ObservableList<NotificationGroup> Groups { get => Read(field); } = [];

    public NotificationAccessStatus AccessStatus => Read(_accessStatus);

    public NotificationState FocusState => Read(_focusState);

    public NotificationMode NotificationMode => Read(_notificationMode);

    public NotificationState DoNotDisturbState => Read(_notificationMode) switch
    {
        NotificationMode.Unrestricted => NotificationState.Off,
        NotificationMode.PriorityOnly or NotificationMode.AlarmsOnly => NotificationState.On,
        _ => NotificationState.Unknown
    };

    public string Error => Read(_error);

    public void RequestAccess()
    {
        _ = RequestAccessAsync();
    }

    internal async Task RequestAccessAsync()
    {
        if (_requesting || _disposed)
            return;
        _requesting = true;
        _reader.Invalidate();
        Set(ref _accessStatus, NotificationAccessStatus.Requesting, nameof(AccessStatus));
        Set(ref _error, "", nameof(Error));
        try
        {
            var status = await _backend.RequestAccessAsync().ConfigureAwait(false);
            Post(() =>
            {
                _requesting = false;
                _reader.Invalidate();
                SetAccess(status);
            });
        }
#pragma warning disable CA1031
        catch (Exception e)
#pragma warning restore CA1031
        {
            if (!WindowsFailure.IsExpected(e))
                _log.Warn("notifications", $"Requesting access failed: {e}");
            Post(() =>
            {
                _requesting = false;
                _reader.Invalidate();
                SetAccess(NotificationAccessStatus.Unavailable);
                Fail(e.Message);
            });
        }
    }

    private IDisposable SchedulePoll()
    {
        return _loop.Schedule(PollInterval, () =>
        {
            if (_disposed)
                return;
            _poll = SchedulePoll();
            _reader.Request();
        });
    }

    private void OnToastsChanged()
    {
        _changes.Change();
        _reader.Request();
    }

    internal Task RefreshAsync()
    {
        return _reader.ReadAsync();
    }

    private NotificationSnapshot Read(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        _unwatch ??= _backend.WatchToasts(OnToastsChanged);
        var focus = _backend.ReadFocus();
        var mode = _backend.ReadMode();
        try
        {
            var access = _backend.ReadAccess();
            if (access != NotificationAccessStatus.Allowed)
            {
                _changes.Change();
                return new NotificationSnapshot(access, [], focus, mode);
            }

            if (!_changes.Due(out var stamp))
                return new NotificationSnapshot(access, _toasts, focus, mode);
#pragma warning disable VSTHRD002
            var listed = _backend.ListToastsAsync(cancellation).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
            cancellation.ThrowIfCancellationRequested();
            access = _backend.ReadAccess();
            if (access != NotificationAccessStatus.Allowed)
            {
                _changes.Change();
                return new NotificationSnapshot(access, [], focus, mode);
            }

            var known = _read;
            Dictionary<(uint, DateTimeOffset), IncomingToast> read = [];
            List<IncomingToast> toasts = [];
            foreach (var listedToast in listed)
                try
                {
                    var key = (listedToast.Id, listedToast.CreatedAt);
                    if (!known.TryGetValue(key, out var toast))
                        toast = listedToast.Read();
                    read[key] = toast;
                    toasts.Add(toast);
                }
                catch (Exception e) when (WindowsFailure.IsExpected(e))
                {
                    _log.Warn("notifications", e.Message);
                }

            _read = read;
            _toasts = toasts;
            _changes.Read(stamp);
            return new NotificationSnapshot(access, toasts, focus, mode);
        }
        catch (Exception e) when (WindowsFailure.IsExpected(e))
        {
            return new NotificationSnapshot(NotificationAccessStatus.Unavailable, [], focus, mode,
                $"Windows notification access is unavailable: {e.Message} Check package identity and the userNotificationListener capability.");
        }
    }

    internal void Apply(NotificationSnapshot snapshot)
    {
        if (_disposed || _requesting)
            return;
        SetAccess(snapshot.AccessStatus);
        Set(ref _focusState, snapshot.Focus, nameof(FocusState));
        var doNotDisturb = DoNotDisturbState;
        Set(ref _notificationMode, snapshot.Mode, nameof(NotificationMode));
        Changed(doNotDisturb != DoNotDisturbState, nameof(DoNotDisturbState));
        if (snapshot.Error.Length > 0)
            Set(ref _error, snapshot.Error, nameof(Error));
        if (snapshot.AccessStatus != NotificationAccessStatus.Allowed)
            return;

        List<Notification> added = [];
        var ordered = snapshot.Notifications.DistinctBy(t => t.Id).OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id);
        Notifications.Sync(ordered, n => n.Id, t => t.Id, t =>
        {
            var notification = new Notification(this, _icons, t);
            added.Add(notification);
            return notification;
        }, (n, t) => n.Update(t), n => n.LetGo());
        SyncGroups();
        foreach (var notification in added)
            NotificationAdded?.Invoke(notification);
    }

    private void SetAccess(NotificationAccessStatus status)
    {
        Set(ref _accessStatus, status, nameof(AccessStatus));
        Set(ref _error, status switch
        {
            NotificationAccessStatus.Denied =>
                "Notification access was denied. Allow access in Windows Settings to use the listener.",
            NotificationAccessStatus.Unavailable =>
                "Notification access is unavailable. A package identity with the userNotificationListener capability may be required.",
            _ => ""
        }, nameof(Error));
        if (status != NotificationAccessStatus.Allowed && status != NotificationAccessStatus.Requesting)
        {
            var removed = Notifications.ToArray();
            Notifications.Clear();
            SyncGroups();
            foreach (var notification in removed)
                notification.LetGo();
        }
    }

    private void SyncGroups()
    {
        var grouped = Notifications.GroupBy(n => n.AppKey).ToArray();
        Groups.Sync(grouped, g => g.Key, g => g.Key, g => new NotificationGroup(this, g.Key),
            (group, items) => group.Update([.. items]));
    }

    public bool ClearAll()
    {
        return DismissMany(Notifications.ToArray());
    }

    internal bool DismissMany(IEnumerable<Notification> notifications)
    {
        if (_disposed)
            return false;
        var success = true;
        string? failure = null;
        foreach (var notification in notifications.ToArray())
        {
            if (!Notifications.Contains(notification))
            {
                success = false;
                continue;
            }

            try
            {
                _backend.Dismiss(notification.Id);
                _changes.Change();
                _reader.Invalidate();
                Notifications.Remove(notification);
                SyncGroups();
                notification.LetGo();
            }
            catch (Exception e) when (WindowsFailure.IsExpected(e))
            {
                success = false;
                failure = e.Message;
            }
        }

        if (failure is not null)
            Fail(failure);
        else if (success)
            Set(ref _error, "", nameof(Error));
        return success;
    }

    internal void Fail(string message)
    {
        Set(ref _error, string.IsNullOrWhiteSpace(message) ? "The Windows notification operation failed." : message,
            nameof(Error));
        OperationFailed?.Invoke(_error);
    }

    private void Post(Action action)
    {
        _loop.Post(() =>
        {
            if (!_disposed)
                action();
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _poll.Dispose();
        _stop.Cancel();
        _stop.Dispose();
        _reader.Stop(() => _unwatch?.Invoke());
        foreach (var notification in Notifications)
            notification.LetGo();
    }
}

public sealed class Notification : ObservableObject
{
    private readonly NotificationListener _listener;
    private readonly NotificationAppIcons _icons;
    private IncomingToast _toast;
    private string _appIcon = "";
    private string? _loadedKey;
    private int _iconRequest;

    internal Notification(NotificationListener listener, NotificationAppIcons icons, IncomingToast toast)
    {
        _listener = listener;
        _icons = icons;
        _toast = toast;
    }

    public uint Id => Read(_toast).Id;

    public string AppId => Read(_toast).AppId;

    public string AppName => Read(_toast).AppName;

    public string AppIcon => Read(_appIcon);

    public string Title => Read(_toast).Title;

    public string Body => Read(_toast).Body;

    public DateTimeOffset CreatedAt => Read(_toast).CreatedAt;

    public DateTimeOffset? ExpiresAt => Read(_toast).ExpiresAt;

    public bool Dismiss()
    {
        return _listener.DismissMany([this]);
    }

    internal string AppKey => AppId.Length > 0 ? AppId : "notification:" + Id.ToString(CultureInfo.InvariantCulture);

    internal void Update(IncomingToast toast)
    {
        var old = _toast;
        _toast = toast;
        Changed(old.AppId != toast.AppId, nameof(AppId));
        Changed(old.AppName != toast.AppName, nameof(AppName));
        Changed(old.Title != toast.Title, nameof(Title));
        Changed(old.Body != toast.Body, nameof(Body));
        Changed(old.CreatedAt != toast.CreatedAt, nameof(CreatedAt));
        Changed(old.ExpiresAt != toast.ExpiresAt, nameof(ExpiresAt));
        SyncIcon();
    }

    private void SyncIcon()
    {
        var source = _toast.Logo;
        var key = source is null ? null : AppKey;
        if (string.Equals(key, _loadedKey, StringComparison.Ordinal))
            return;
        _loadedKey = key;
        LetGo();
        if (source is null || key is null)
            return;
        var request = _iconRequest;
        var label = _toast.AppName.Length > 0 ? _toast.AppName :
            _toast.AppId.Length > 0 ? _toast.AppId : "a notification";
        _icons.Read(key, label, source, url => ApplyIcon(url, request));
    }

    private void ApplyIcon(string url, int request)
    {
        if (request != _iconRequest)
        {
            TempImageStore.Shared.ReleaseLater(url);
            return;
        }

        Set(ref _appIcon, url, nameof(AppIcon));
    }

    internal void LetGo()
    {
        _iconRequest++;
        var url = _appIcon;
        Set(ref _appIcon, "", nameof(AppIcon));
        TempImageStore.Shared.ReleaseLater(url);
    }
}

public sealed class NotificationGroup : ObservableObject
{
    private readonly NotificationListener _listener;
    private string _appId = "";
    private string _appName = "";

    internal NotificationGroup(NotificationListener listener, string key)
    {
        _listener = listener;
        Key = key;
    }

    internal string Key { get; }

    public string AppId => Read(_appId);

    public string AppName => Read(_appName);

    public ObservableList<Notification> Notifications { get => Read(field); } = [];

    public bool Dismiss()
    {
        return _listener.DismissMany(Notifications.ToArray());
    }

    internal void Update(Notification[] items)
    {
        Set(ref _appId, items[0].AppId, nameof(AppId));
        Set(ref _appName, items[0].AppName, nameof(AppName));
        Notifications.Mirror(items);
    }
}
