namespace Winshell.Services.Tests;

[TestClass]
public sealed class FailureWarningTests
{
    [TestMethod]
    public void A_failure_that_lasts_is_warned_once_until_a_poll_succeeds()
    {
        var messages = new List<string>();
        var log = new Log { Sink = (_, category, message) => messages.Add($"{category}: {message}") };
        var warning = new FailureWarning(log, "bluetooth");

        warning.Failed(new InvalidOperationException("radio broke"));
        warning.Failed(new InvalidOperationException("radio broke"));
        warning.Failed(new IOException("radio broke"));
        Assert.AreSequenceEqual(
            ["bluetooth: InvalidOperationException: radio broke", "bluetooth: IOException: radio broke"],
            messages);

        warning.Failed(new InvalidOperationException("radio gone"));
        warning.Succeeded();
        warning.Failed(new InvalidOperationException("radio gone"));
        Assert.AreSequenceEqual(
        [
            "bluetooth: InvalidOperationException: radio broke",
            "bluetooth: IOException: radio broke",
            "bluetooth: InvalidOperationException: radio gone",
            "bluetooth: InvalidOperationException: radio gone"
        ], messages);
    }
}
