namespace Winshell.Notifications;

internal interface INotificationBackend
{
    Task<NotificationAccessStatus> RequestAccessAsync();

    NotificationAccessStatus ReadAccess();

    NotificationState ReadFocus();

    NotificationMode ReadMode();

    string? ToastStamp();

    Task<IReadOnlyList<ListedToast>> ListToastsAsync(CancellationToken cancellation);

    void Dismiss(uint id);

    Action? WatchToasts(Action changed);

    void Show(string appId, string xml, string tag, string group, Action<string> failed);
}

internal sealed record ListedToast(uint Id, DateTimeOffset CreatedAt, Func<IncomingToast> Read);
