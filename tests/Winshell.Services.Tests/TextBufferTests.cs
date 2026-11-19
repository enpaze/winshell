using Winshell.Native;
using WIN32_ERROR = Windows.Win32.Foundation.WIN32_ERROR;

namespace Winshell.Services.Tests;

[TestClass]
public class TextBufferTests
{
    private const int First = 1024, Max = 32_767;

    [TestMethod]
    public void A_text_longer_than_the_first_buffer_is_read_on_the_retry()
    {
        var path = new string('a', 2000);
        var calls = 0;
        var read = TextBuffer.Read(First, Max, buffer =>
        {
            calls++;
            if (buffer.Length < path.Length)
                return new TextBuffer.Answer(WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER, path.Length + 1);
            path.CopyTo(buffer);
            return new TextBuffer.Answer(WIN32_ERROR.ERROR_SUCCESS, path.Length);
        });

        Assert.AreEqual(2, calls);
        Assert.AreEqual(path, read);
    }

    [TestMethod]
    public void A_query_that_fails_for_another_reason_returns_nothing()
    {
        Assert.IsNull(TextBuffer.Read(First, Max, _ => new TextBuffer.Answer(WIN32_ERROR.ERROR_ACCESS_DENIED, 0)));
    }

    [TestMethod]
    public void A_required_size_past_the_limit_returns_nothing()
    {
        Assert.IsNull(TextBuffer.Read(First, Max, _ =>
            new TextBuffer.Answer(WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER, Max + 1)));
    }

    [TestMethod]
    public void A_second_short_buffer_returns_nothing()
    {
        Assert.IsNull(TextBuffer.Read(First, Max, buffer =>
            new TextBuffer.Answer(WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER, buffer.Length + 1)));
    }
}
