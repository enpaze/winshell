using Winshell.Notifications;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class NotificationWatchTests
{
    [TestMethod]
    public async Task A_toast_change_refreshes_without_waiting_for_the_poll_and_dispose_stops_the_watch()
    {
        var backend = new WatchingBackend();
        var listener = new NotificationsService(new ManualEventLoop(), Log.Default, backend);
        try
        {
            await backend.FirstRead.Task.WaitAsync(TimeSpan.FromSeconds(5), CurrentTest.Cancellation);

            Assert.IsNotNull(backend.Changed);
            backend.Changed();
            await backend.SecondRead.Task.WaitAsync(TimeSpan.FromSeconds(5), CurrentTest.Cancellation);
        }
        finally
        {
            listener.Stop();
        }

        await backend.Unwatched.Task.WaitAsync(TimeSpan.FromSeconds(5), CurrentTest.Cancellation);
        Assert.IsNull(backend.Changed);
    }

    private sealed class WatchingBackend : INotificationBackend
    {
        private int _reads;
        public TaskCompletionSource FirstRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Unwatched { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Action? Changed { get; private set; }

        public Action WatchToasts(Action changed)
        {
            Changed = changed;
            return () =>
            {
                Changed = null;
                Unwatched.TrySetResult();
            };
        }

        public Task<IReadOnlyList<ListedToast>> ListToastsAsync(CancellationToken cancellation)
        {
            (Interlocked.Increment(ref _reads) == 1 ? FirstRead : SecondRead).TrySetResult();
            return Task.FromResult<IReadOnlyList<ListedToast>>([]);
        }

        public string ToastStamp()
        {
            return "unchanged";
        }

        public NotificationAccessStatus ReadAccess()
        {
            return NotificationAccessStatus.Allowed;
        }

        public NotificationState ReadFocus()
        {
            return NotificationState.Off;
        }

        public NotificationMode ReadMode()
        {
            return NotificationMode.Unrestricted;
        }

        public Task<NotificationAccessStatus> RequestAccessAsync()
        {
            return Task.FromResult(NotificationAccessStatus.Allowed);
        }

        public void Dismiss(uint id)
        {
        }

        public void Show(string appId, string xml, string tag, string group, Action<string> failed)
        {
        }
    }
}
