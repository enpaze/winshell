using System.Globalization;
using Windows.ApplicationModel;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using Windows.UI.Shell;

namespace Winshell.Notifications;

internal sealed class NotificationBackend : INotificationBackend
{
    private static readonly string Database =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows",
            "Notifications", "wpndatabase.db");

    public async Task<NotificationAccessStatus> RequestAccessAsync()
    {
        return NotificationListenerAccess.Map(await UserNotificationListener.Current.RequestAccessAsync());
    }

    public NotificationAccessStatus ReadAccess()
    {
        return NotificationListenerAccess.Map(UserNotificationListener.Current.GetAccessStatus());
    }

    public NotificationState ReadFocus()
    {
        try
        {
            if (ApiInformation.IsTypePresent("Windows.UI.Shell.FocusSessionManager") && FocusSessionManager.IsSupported)
                return FocusSessionManager.GetDefault().IsFocusActive ? NotificationState.On : NotificationState.Off;
        }
        catch (Exception e) when (WindowsFailure.IsExpected(e))
        {
        }

        return NotificationState.Unknown;
    }

    public NotificationMode ReadMode()
    {
        try
        {
            if (ApiInformation.IsPropertyPresent("Windows.UI.Notifications.ToastNotificationManagerForUser",
                    "NotificationMode"))
                return MapMode((int)ToastNotificationManager.GetDefault().NotificationMode);
        }
        catch (Exception e) when (WindowsFailure.IsExpected(e))
        {
        }

        return NotificationMode.Unknown;
    }

    public string? ToastStamp()
    {
        try
        {
            var database = new FileInfo(Database);
            var log = new FileInfo(Database + "-wal");
            if (!database.Exists)
                return null;
            return string.Create(CultureInfo.InvariantCulture,
                $"{database.Length} {database.LastWriteTimeUtc.Ticks} {(log.Exists ? log.Length : -1)} {(log.Exists ? log.LastWriteTimeUtc.Ticks : 0)}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ListedToast>> ListToastsAsync(CancellationToken cancellation)
    {
        var notifications = await UserNotificationListener.Current.GetNotificationsAsync(NotificationKinds.Toast)
            .AsTask(cancellation).ConfigureAwait(false);
        return [.. notifications.Select(n => new ListedToast(n.Id, n.CreationTime, () => ReadToast(n)))];
    }

    public void Dismiss(uint id)
    {
        var listener = UserNotificationListener.Current;
        if (listener.GetAccessStatus() != UserNotificationListenerAccessStatus.Allowed)
            throw new UnauthorizedAccessException("Windows notification listener access is no longer allowed.");
        listener.RemoveNotification(id);
    }

    public Action? WatchToasts(Action changed)
    {
        if (!Packaging.IsPackaged)
            return null;
        try
        {
            var listener = UserNotificationListener.Current;
            if (listener.GetAccessStatus() != UserNotificationListenerAccessStatus.Allowed)
                return null;
            TypedEventHandler<UserNotificationListener, UserNotificationChangedEventArgs> handler = (_, _) => changed();
            listener.NotificationChanged += handler;
            return () => listener.NotificationChanged -= handler;
        }
        catch (Exception e) when (WindowsFailure.IsExpected(e))
        {
            return null;
        }
    }

    public void Show(string appId, string xml, string tag, string group, Action<string> failed)
    {
        var content = new XmlDocument();
        content.LoadXml(xml);
        var toast = new ToastNotification(content);
        toast.Failed += (_, args) => failed($"Windows rejected the toast: {args.ErrorCode.Message}");
        if (tag.Length > 0)
            toast.Tag = tag;
        if (group.Length > 0)
            toast.Group = group;
        var notifier = ToastNotificationManager.CreateToastNotifier(appId);
        if (notifier.Setting != NotificationSetting.Enabled)
            throw new WindowsRefusal(
                $"Windows disabled toast notifications for '{appId}': {notifier.Setting}.");
        notifier.Show(toast);
    }

    private static IncomingToast ReadToast(UserNotification notification)
    {
        var binding = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric)
                      ?? notification.Notification.Visual.Bindings.FirstOrDefault();
        var texts = binding?.GetTextElements().Select(t => t.Text).ToArray() ?? [];
        var appId = "";
        var appName = "";
        IAppLogoSource? logo = null;
        try
        {
            var info = notification.AppInfo;
            appId = info.AppUserModelId;
            var display = info.DisplayInfo;
            appName = display.DisplayName;
            logo = new WindowsAppLogo(display);
        }
        catch (Exception e) when (WindowsFailure.IsExpected(e))
        {
        }

        return new IncomingToast(notification.Id, appId, appName, texts.FirstOrDefault() ?? "",
            string.Join('\n', texts.Skip(1)), notification.CreationTime, notification.Notification.ExpirationTime,
            logo);
    }

    internal static NotificationMode MapMode(int mode)
    {
        return mode switch
        {
            0 => NotificationMode.Unrestricted,
            1 => NotificationMode.PriorityOnly,
            2 => NotificationMode.AlarmsOnly,
            _ => NotificationMode.Unknown
        };
    }
}

internal sealed class WindowsAppLogo(AppDisplayInfo display) : IAppLogoSource
{
    private const int Side = 256;

    public Stream? Open(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var reference = display.GetLogo(new Size(Side, Side));
        if (reference is null)
            return null;
#pragma warning disable VSTHRD002
        var opened = reference.OpenReadAsync().AsTask(cancellation).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        return opened is null ? null : new WinRtReadStream(opened);
    }

    private sealed class WinRtReadStream(IRandomAccessStream inner) : Stream
    {
        private readonly Stream _stream = inner.AsStreamForRead();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream.Dispose();
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
