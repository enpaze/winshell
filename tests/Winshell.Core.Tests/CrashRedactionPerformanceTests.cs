namespace Winshell.Core.Tests;

[TestClass]
public sealed class CrashRedactionPerformanceTests
{
    [TestMethod]
    public async Task A_long_log_line_does_not_block_logging()
    {
        var text = new string('a', 100_000);

        var redacted = await Task.Run(() => CrashRedaction.Redact(text))
            .WaitAsync(TimeSpan.FromSeconds(30), CurrentTest.Cancellation);

        Assert.AreEqual(text, redacted);
    }
}
