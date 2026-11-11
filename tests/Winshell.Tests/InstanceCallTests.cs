using System.IO.Pipes;
using System.Text;

namespace Winshell.Tests;

/// <summary>
/// A call to an instance that fails says which instance and why. The test is the shell: it serves the pipe here, so no
/// instance on this PC is called.
/// </summary>
public class InstanceCallTests
{
    private readonly string _pipe = "winshell-test-" + Guid.NewGuid().ToString("N");

    private InstanceInfo Instance =>
        new("abc123", Environment.ProcessId, "shell", @"C:\cfg\MyShell.exe",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), _pipe, "log.txt");

    private static readonly InstanceCall.Answer ClosedWithoutAnswering =
        new(null, "Instance abc123 closed the control pipe without answering.");

    // The call writes its request and then reads, with no await between the two. One shell writes nothing back and
    // shuts the pipe, so that read is given no line. The other shuts it only once that read has started. Both are
    // the same sentence.
    [Fact]
    public async Task A_pipe_closed_with_nothing_written_back_says_it_closed_without_answering()
    {
        await using var shell = IpcServer.OpenFirst(_pipe);
        var closed = CloseWithNothingWrittenBack(shell);

        var answer = await Ask();

        Assert.Equal(ClosedWithoutAnswering, answer);
        await closed;
    }

    [Fact]
    public async Task A_pipe_closed_while_the_call_is_reading_says_it_closed_without_answering()
    {
        await using var shell = IpcServer.OpenFirst(_pipe);
        var closed = CloseWhileTheCallIsReading(shell);

        var answer = await Ask();

        Assert.Equal(ClosedWithoutAnswering, answer);
        await closed;
    }

    [Fact]
    public async Task A_call_this_winshell_rejects_names_the_instance_and_the_reason()
    {
        await using var shell = IpcServer.OpenFirst(_pipe);
        var other = ProductVersion.Current.Major == 9 ? "1.0.0" : "9.9.9";
        var answering = ReadThen(shell, () =>
        {
            // The write runs, and is awaited, before this pipe is disposed.
            using var writer = new StreamWriter(shell, new UTF8Encoding(false), leaveOpen: true);
            writer.WriteLine($$"""{"ok":true,"winshell":"{{other}}"}""");
        });

        var answer =
            await InstanceCall.AskAsync(Instance, IpcRequest.ForShow(false), TestContext.Current.CancellationToken);

        Assert.Equal(
            new InstanceCall.Answer(null,
                $"Rejected instance abc123: {IpcProtocol.ReleaseRefusal(other, ProductVersion.Current.Text)}"),
            answer);
        await answering;
    }

    private Task<InstanceCall.Answer> Ask()
    {
        return InstanceCall.AskAsync(Instance, IpcRequest.ForShow(false), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Shuts the pipe once the request is in, and writes nothing back. Closing earlier fails the call's write, so the
    /// call says it could not reach the instance. There is no await between that write and the call's read, so this
    /// is the close that meets the read.
    /// </summary>
    private static async Task CloseWithNothingWrittenBack(NamedPipeServerStream shell)
    {
        await TakeRequest(shell);
        shell.Dispose();
    }

    /// <summary>
    /// Writes <c>null</c> with no newline, waits until the call's read has pulled those bytes, and only then shuts
    /// the pipe. The read cannot have finished: a line needs a newline or the end of the pipe. <c>null</c> is not an
    /// answer, so the call reads again and finds the pipe shut.
    /// </summary>
    private static async Task CloseWhileTheCallIsReading(NamedPipeServerStream shell)
    {
        var cancel = TestContext.Current.CancellationToken;
        await TakeRequest(shell);
        await using (var writer = new StreamWriter(shell, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true })
        {
            await writer.WriteAsync("null".AsMemory(), cancel);
        }

        // WaitForPipeDrain blocks in Windows until the other end has read. OwnThread keeps that off the pool.
        // The drain takes no token, so the wait does: a cancelled call drops its end and the drain returns.
        await OwnThread.Run(shell.WaitForPipeDrain).WaitAsync(cancel);
        await shell.DisposeAsync();
    }

    private static async Task TakeRequest(NamedPipeServerStream shell)
    {
        var cancel = TestContext.Current.CancellationToken;
        await shell.WaitForConnectionAsync(cancel);
        using var reader = new StreamReader(shell, Encoding.UTF8, detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        await reader.ReadLineAsync(cancel);
    }

    /// <summary>Takes the one request, then does what the shell does next.</summary>
    private static async Task ReadThen(NamedPipeServerStream shell, Action next)
    {
        await shell.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        var reader = new StreamReader(shell, Encoding.UTF8, leaveOpen: true);
        await reader.ReadLineAsync(TestContext.Current.CancellationToken);
        next();
    }
}
