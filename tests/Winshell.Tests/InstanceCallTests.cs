using System.IO.Pipes;
using System.Text;

namespace Winshell.Tests;

[TestClass]
public class InstanceCallTests
{
    private readonly string _pipe = "winshell-test-" + Guid.NewGuid().ToString("N");

    private InstanceInfo Instance =>
        new("abc123", Environment.ProcessId, "shell", @"C:\cfg\MyShell.exe",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), _pipe, "log.txt");

    private static readonly InstanceCall.Answer ClosedWithoutAnswering =
        new(null, "Instance abc123 closed the control pipe without answering.");

    [TestMethod]
    public async Task A_pipe_closed_with_nothing_written_back_says_it_closed_without_answering()
    {
        await using var shell = IpcServer.OpenFirst(_pipe);
        var closed = CloseWithNothingWrittenBack(shell);

        var answer = await Ask();

        Assert.AreEqual(ClosedWithoutAnswering, answer);
        await closed;
    }

    [TestMethod]
    public async Task A_pipe_closed_while_the_call_is_reading_says_it_closed_without_answering()
    {
        await using var shell = IpcServer.OpenFirst(_pipe);
        var closed = CloseWhileTheCallIsReading(shell);

        var answer = await Ask();

        Assert.AreEqual(ClosedWithoutAnswering, answer);
        await closed;
    }

    [TestMethod]
    public async Task A_call_this_winshell_rejects_names_the_instance_and_the_reason()
    {
        await using var shell = IpcServer.OpenFirst(_pipe);
        var other = ProductVersion.Current.Major == 9 ? "1.0.0" : "9.9.9";
        var answering = ReadThen(shell, () =>
        {
            using var writer = new StreamWriter(shell, new UTF8Encoding(false), leaveOpen: true);
            writer.WriteLine($$"""{"ok":true,"winshell":"{{other}}"}""");
        });

        var answer =
            await InstanceCall.AskAsync(Instance, IpcRequest.ForShow(false), CurrentTest.Cancellation);

        Assert.AreEqual(
            new InstanceCall.Answer(null,
                $"Rejected instance abc123: {IpcProtocol.ReleaseRefusal(other, ProductVersion.Current.Text)}"),
            answer);
        await answering;
    }

    private Task<InstanceCall.Answer> Ask()
    {
        return InstanceCall.AskAsync(Instance, IpcRequest.ForShow(false), CurrentTest.Cancellation);
    }

    private static async Task CloseWithNothingWrittenBack(NamedPipeServerStream shell)
    {
        await TakeRequest(shell);
        shell.Dispose();
    }

    private static async Task CloseWhileTheCallIsReading(NamedPipeServerStream shell)
    {
        var cancel = CurrentTest.Cancellation;
        await TakeRequest(shell);
        await using (var writer = new StreamWriter(shell, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true })
        {
            await writer.WriteAsync("null".AsMemory(), cancel);
        }

        await OwnThread.Run(shell.WaitForPipeDrain).WaitAsync(cancel);
        await shell.DisposeAsync();
    }

    private static async Task TakeRequest(NamedPipeServerStream shell)
    {
        var cancel = CurrentTest.Cancellation;
        await shell.WaitForConnectionAsync(cancel);
        using var reader = new StreamReader(shell, Encoding.UTF8, detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        await reader.ReadLineAsync(cancel);
    }

    private static async Task ReadThen(NamedPipeServerStream shell, Action next)
    {
        await shell.WaitForConnectionAsync(CurrentTest.Cancellation);
        var reader = new StreamReader(shell, Encoding.UTF8, leaveOpen: true);
        await reader.ReadLineAsync(CurrentTest.Cancellation);
        next();
    }
}
