using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace Winshell.Tests;

[TestClass]
public sealed partial class ShellExitTests : IDisposable
{
    private readonly string _pipe = "winshell-test-" + Guid.NewGuid().ToString("N");

    private readonly Process _process = Process.Start(
        new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true
        })!;

    private readonly ExitIntentEvents _supervision;

    private static readonly QuitPatience Patient = new(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    public ShellExitTests()
    {
        _supervision = new ExitIntentEvents(_process.Id);
    }

    private InstanceInfo Instance => new(
        "abc123", _process.Id, "shell", @"C:\cfg\MyShell.exe", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        _pipe, "log.txt",
        _process.StartTime.ToUniversalTime().Ticks, ProcessName: _process.ProcessName);

    [TestMethod]
    public async Task A_shell_that_exits_before_it_answers_keeps_its_reload()
    {
        await using var pipe = IpcServer.OpenFirst(_pipe);
        var asked = ReadRequest(pipe);
        var quit = ShellExit.QuitAsync(Instance, ExitIntent.Reload, Patient, ShellExit.Ending.Never);
        await asked;

#pragma warning disable CsWinRT1030
        await ExitAsync(pipe);
#pragma warning restore CsWinRT1030

        Assert.IsTrue((await quit).Exited);
        Assert.AreEqual(ExitIntent.Reload, _supervision.Intent);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task A_stop_outranks_a_reload_in_either_order(bool stopFirst)
    {
        var (first, second) = stopFirst ? (ExitIntent.Stop, ExitIntent.Reload) : (ExitIntent.Reload, ExitIntent.Stop);
        await using var firstPipe = IpcServer.OpenFirst(_pipe);
        var firstAsked = ReadRequest(firstPipe);
        var firstQuit = ShellExit.QuitAsync(Instance, first, Patient, ShellExit.Ending.Never);
        await firstAsked;
        await using var secondPipe = IpcServer.OpenNext(_pipe);
        var secondAsked = ReadRequest(secondPipe);
        var secondQuit = ShellExit.QuitAsync(Instance, second, Patient, ShellExit.Ending.Never);
        await secondAsked;

#pragma warning disable CsWinRT1030
        await ExitAsync(firstPipe, secondPipe);
#pragma warning restore CsWinRT1030

        Assert.IsTrue((await firstQuit).Exited);
        Assert.IsTrue((await secondQuit).Exited);
        Assert.AreEqual(ExitIntent.Stop, _supervision.Intent);
    }

    private static async Task ReadRequest(NamedPipeServerStream pipe)
    {
        await pipe.WaitForConnectionAsync(CurrentTest.Cancellation);
        var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        await reader.ReadLineAsync(CurrentTest.Cancellation);
    }

    private async Task ExitAsync(params NamedPipeServerStream[] pipes)
    {
        _process.Kill();
        await _process.WaitForExitAsync(CurrentTest.Cancellation);
        foreach (var pipe in pipes)
            await pipe.DisposeAsync();
    }

    public void Dispose()
    {
        if (!_process.HasExited)
        {
            _process.Kill();
            _process.WaitForExit(5000);
        }

        _supervision.Dispose();
        _process.Dispose();
    }
}
