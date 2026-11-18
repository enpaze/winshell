using Winshell.Native;

namespace Winshell.Services.Tests;

[TestClass]
public class ProcessImageTests
{
    [TestMethod]
    public void This_process_image_is_the_running_program()
    {
        var path = ProcessImage.TryRead((uint)Environment.ProcessId);
        Assert.AreEqual(Environment.ProcessPath, path, StringComparer.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void An_unknown_process_has_no_image()
    {
        Assert.IsNull(ProcessImage.TryRead(0));
    }
}
