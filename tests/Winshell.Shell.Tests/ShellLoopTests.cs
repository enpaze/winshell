using Winshell.Capture;

namespace Winshell.ShellTests;

[TestClass]
[TestCategory("Window")]
public sealed class ShellLoopTests
{
    [TestMethod]
    public async Task A_capture_session_made_in_a_running_shell_raises_its_frames_on_the_shell_loop()
    {
        var (shellThread, frameThread) = await ShellFixture.On(async shell =>
        {
            using var window = shell.Add(new FloatingWindow { Title = "Winshell test", Width = 320, Height = 200 });
            using var session = new CaptureSession(shell.Log) { Target = CaptureTarget.ForWindow(window.Host!.Handle) };
            var frame = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            session.FrameReady += () => frame.TrySetResult(Environment.CurrentManagedThreadId);

            Assert.IsTrue(session.Start(), session.Error);
            var raisedOn = await frame.Task.WaitAsync(TimeSpan.FromSeconds(20), CurrentTest.Cancellation);
            return (Environment.CurrentManagedThreadId, raisedOn);
        });

        Assert.AreEqual(shellThread, frameThread);
    }
}
