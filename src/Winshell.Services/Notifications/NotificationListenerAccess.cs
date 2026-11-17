using Windows.UI.Notifications.Management;

namespace Winshell.Notifications;

internal static class NotificationListenerAccess
{
    public static NotificationAccessStatus Read()
    {
        try
        {
            return Map(UserNotificationListener.Current.GetAccessStatus());
        }
        catch (Exception e) when (WindowsFailure.IsExpected(e))
        {
            return NotificationAccessStatus.Unavailable;
        }
    }

    public static NotificationAccessStatus Map(UserNotificationListenerAccessStatus access)
    {
        return access switch
        {
            UserNotificationListenerAccessStatus.Allowed => NotificationAccessStatus.Allowed,
            UserNotificationListenerAccessStatus.Denied => NotificationAccessStatus.Denied,
            _ => NotificationAccessStatus.Unspecified
        };
    }
}
