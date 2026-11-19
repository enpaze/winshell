using Winshell.Native;
using Winshell.AppWindows;

namespace Winshell.Services.Tests;

public sealed class WindowScanTests
{
    [Fact]
    public async Task A_hidden_window_on_a_thread_that_is_not_answering_is_reported_as_not_visible()
    {
        using var release = new ManualResetEventSlim();
        var made = new TaskCompletionSource<nint>(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new Thread(() =>
            {
                var hwnd = NativeWindow.OfClass("STATIC", "", 0, 0, 0, 1, 1, 0, PInvoke.GetModuleHandle(null));
                made.SetResult(hwnd);
                release.Wait();
                PInvoke.DestroyWindow((HWND)hwnd);
            })
        { IsBackground = true };
        owner.Start();
        var window = await made.Task.WaitAsync(TestContext.Current.CancellationToken);

        try
        {
            var facts = await Task.Run(() => WindowBackend.Shared.TaskbarFacts(window),
                    TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            Assert.True(facts.Exists);
            Assert.False(facts.Visible);
        }
        finally
        {
            release.Set();
            Assert.True(owner.Join(TimeSpan.FromSeconds(10)), "The hidden window's owner did not exit.");
        }
    }
}
