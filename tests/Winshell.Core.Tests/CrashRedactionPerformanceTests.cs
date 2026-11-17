namespace Winshell.Core.Tests;

public sealed class CrashRedactionPerformanceTests
{
    [Fact]
    public async Task A_long_log_line_does_not_block_logging()
    {
        var text = new string('a', 100_000);

        var redacted = await Task.Run(() => CrashRedaction.Redact(text))
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(text, redacted);
    }
}
