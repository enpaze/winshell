using Winshell.Notifications;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class NotificationMappingTests
{
    [TestMethod]
    [DataRow(0, NotificationMode.Unrestricted)]
    [DataRow(1, NotificationMode.PriorityOnly)]
    [DataRow(2, NotificationMode.AlarmsOnly)]
    [DataRow(19, NotificationMode.Unknown)]
    public void Native_notification_mode_mapping_preserves_unknown_values(int native, NotificationMode expected)
    {
        Assert.AreEqual(expected, NotificationBackend.MapMode(native));
    }
}
