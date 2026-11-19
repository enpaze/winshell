using Winshell.Native;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class CallbackTests : IDisposable
{
    private const uint Probe = 0x0013;
    private readonly List<NativeWindow> _windows = [];

    [TestMethod]
    public void A_throwing_handler_reaches_the_reporter_and_Windows_gets_the_default_answer()
    {
        var thrown = new InvalidOperationException("broken");
        var reported = new List<Exception>();
        var window = Open(thrown, reported.Add);

        Assert.AreEqual(1, (nint)PInvoke.SendMessage((HWND)window.Handle, Probe, 0, 0));
        Assert.AreSame(thrown, Assert.ContainsSingle(reported));
    }

    [TestMethod]
    public void A_failing_reporter_does_not_reach_Windows()
    {
        var window = Open(new InvalidOperationException("broken"), _ => throw new IOException("disk full"));

        Assert.AreEqual(1, (nint)PInvoke.SendMessage((HWND)window.Handle, Probe, 0, 0));
    }

    private NativeWindow Open(Exception thrown, Action<Exception> failed)
    {
        var window = NativeWindow.TryCreate("WinshellTestCallback", 0, 0,
            message => message.Id == Probe ? throw thrown : null, failed, true);
        Assert.IsNotNull(window);
        _windows.Add(window);
        return window;
    }

    public void Dispose()
    {
        foreach (var window in _windows)
            window.Release(true);
    }
}
