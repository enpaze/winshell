using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using Winshell.Native;

namespace Winshell.Winland;

internal sealed class WinlandBackend : IWinlandBackend
{
    private static readonly TimeSpan FirstPoll = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LastPoll = TimeSpan.FromSeconds(30);
    private static readonly int SessionId = Process.GetCurrentProcess().SessionId;

    private static readonly IReadOnlyList<string> Candidates =
        WinlandProtocol.PipeNames(Environment.GetEnvironmentVariable("WINLAND_PIPE"), SessionId);

    public static WinlandBackend Shared { get; } = new(TimeProvider.System);

    private readonly TimeProvider _time;
    private readonly Listeners<Handlers> _listeners = new();
    private readonly IReadOnlyList<string> _candidates;
    private TimeSpan _poll = FirstPoll;
    private int _started;
    private volatile string? _pipeName;
    private volatile int _processId;
    private volatile bool _connected;

    internal WinlandBackend(TimeProvider time, IReadOnlyList<string>? pipes = null)
    {
        _time = time;
        _candidates = pipes ?? Candidates;
    }

    public bool IsConnected => _connected;

    public int ProcessId => _processId;

    public string PipePath => @"\\.\pipe\" + (_pipeName ?? _candidates[0]);

    public IDisposable Listen(Action<bool> connectionChanged, Action<string> eventReceived, Action<Exception> failed)
    {
        var listening = _listeners.Add(new Handlers(connectionChanged, eventReceived), failed);
        if (Interlocked.Exchange(ref _started, 1) == 0)
            _ = Task.Run(FollowEvents);
        return listening;
    }

    private async Task FollowEvents()
    {
        while (true)
        {
            var lost = false;
            foreach (var name in _candidates)
            {
                var pipe = await ConnectAsync(name, 250).ConfigureAwait(false);
                if (pipe is null)
                    continue;
                _pipeName = name;
                _processId = ServerProcess(pipe) is { } pid ? (int)pid : 0;
                try
                {
                    await pipe.WriteAsync("events\n"u8.ToArray()).ConfigureAwait(false);
                    await pipe.FlushAsync().ConfigureAwait(false);
                    _connected = true;
                    _listeners.Raise(true, static (handlers, connected) => handlers.ConnectionChanged(connected));
                    var reader = new WinlandReader(pipe);
                    while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
                        if (line.Length > 0)
                            _listeners.Raise(line, static (handlers, received) => handlers.EventReceived(received));
                }
                catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidDataException
                                              or InvalidOperationException)
                {
                }
                finally
                {
                    await pipe.DisposeAsync().ConfigureAwait(false);
                    _processId = 0;
                    if (_connected)
                    {
                        _connected = false;
                        _listeners.Raise(false, static (handlers, connected) => handlers.ConnectionChanged(connected));
                    }
                }

                lost = true;
                break;
            }

            await WaitBeforeLookingAgain(lost).ConfigureAwait(false);
        }
    }

    internal async Task WaitBeforeLookingAgain(bool lost, CancellationToken cancel = default)
    {
        var wait = lost ? TimeSpan.FromMilliseconds(250) : _poll;
        _poll = lost ? FirstPoll : NextPoll(_poll);
        await Task.Delay(wait, _time, cancel).ConfigureAwait(false);
    }

    private static TimeSpan NextPoll(TimeSpan poll)
    {
        return poll * 2 < LastPoll ? poll * 2 : LastPoll;
    }

    public async Task<string?> RequestAsync(string command)
    {
        var names = _pipeName is { } known ? [known] : _candidates;
        foreach (var name in names)
        {
            await using var pipe = await ConnectAsync(name, 1000).ConfigureAwait(false);
            if (pipe is null)
                continue;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await pipe.WriteAsync(Encoding.UTF8.GetBytes(command.ReplaceLineEndings(" ") + "\n"), timeout.Token)
                    .ConfigureAwait(false);
                await pipe.FlushAsync(timeout.Token).ConfigureAwait(false);
                return await new WinlandReader(pipe).ReadReplyAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException
                                          or InvalidDataException)
            {
                return null;
            }
        }

        return null;
    }

    private static async Task<NamedPipeClientStream?> ConnectAsync(string name, int timeoutMs)
    {
        var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(timeoutMs).ConfigureAwait(false);
            if (ServerProcess(pipe) is not { } pid || ProcessSession.Of(pid) is not { } session || session != SessionId)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
                return null;
            }

            return pipe;
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException
                                      or OperationCanceledException)
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            return null;
        }
    }

    private static uint? ServerProcess(NamedPipeClientStream pipe)
    {
        return PInvoke.GetNamedPipeServerProcessId(new HANDLE(pipe.SafePipeHandle.DangerousGetHandle()), out var pid)
            ? pid
            : null;
    }

    private sealed record Handlers(Action<bool> ConnectionChanged, Action<string> EventReceived);
}
