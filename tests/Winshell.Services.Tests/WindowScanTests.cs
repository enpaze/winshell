using Winshell.Native;
using Winshell.Windows;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class WindowScanTests
{
    [TestMethod]
    public async Task A_hidden_window_on_a_thread_that_is_not_answering_is_reported_as_not_visible()
    {
        using var release = new ManualResetEventSlim();
        var made = new TaskCompletionSource<nint>(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new Thread(() =>
            {
                var hwnd = NativeWindow.OfClass("STATIC", "", 0, 0, 0, 1, 1, 0, PInvoke.GetModuleHandle(null));
                made.SetResult(hwnd);
                release.Wait(CurrentTest.Cancellation);
                PInvoke.DestroyWindow((HWND)hwnd);
            })
        { IsBackground = true };
        owner.Start();
        var window = await made.Task.WaitAsync(CurrentTest.Cancellation);

        try
        {
            var facts = await Task.Run(() => WindowBackend.Shared.TaskbarFacts(window),
                    CurrentTest.Cancellation)
                .WaitAsync(TimeSpan.FromSeconds(10), CurrentTest.Cancellation);

            Assert.IsTrue(facts.Exists);
            Assert.IsFalse(facts.Visible);
        }
        finally
        {
            release.Set();
            Assert.IsTrue(owner.Join(TimeSpan.FromSeconds(10)), "The hidden window's owner did not exit.");
        }
    }
}
