using System.Buffers;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Win32.SafeHandles;

namespace Winshell;

internal sealed class IpcServer : IDisposable
{
    private readonly string _pipe;
    private readonly IEventLoop _loop;
    private readonly Log _log;
    private readonly IInstanceControl _control;
    private readonly Func<SafePipeHandle, string?> _refusal;
    private readonly CancellationTokenSource _cancel = new();

    private const int WaitingInstances = 2;
    private const int OpenAttempts = 3;

    internal const int MaxRequestBytes = 256 * 1024;

    internal const int MaxClients = 64;

    internal const int MaxQueuedSignals = 1024;

    internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private readonly Lock _firstGate = new();
    private readonly Lock _limit = new();
    private readonly List<NamedPipeServerStream> _serving = [];
    private NamedPipeServerStream? _first;
    private int _clients;
    private bool _disposed;

    public IpcServer(NamedPipeServerStream first, string pipe, IEventLoop loop, Log log, IInstanceControl control,
        Func<SafePipeHandle, string?>? refusal = null)
    {
        _pipe = pipe;
        _first = first;
        _loop = loop;
        _log = log;
        _control = control;
        _refusal = refusal ?? PipeTrust.ClientRefusal;
        _ = Task.Run(AcceptLoop);
    }

    internal static NamedPipeServerStream OpenFirst(string pipe)
    {
        return Create(pipe, true);
    }

    internal static NamedPipeServerStream OpenNext(string pipe)
    {
        return Create(pipe, false);
    }

    internal static IReadOnlyList<string> Candidates(string pipe)
    {
        var names = new string[8];
        names[0] = pipe;
        for (var n = 1; n < names.Length; n++)
            names[n] = pipe + "-" + (n + 1);
        return names;
    }

    internal static NamedPipeServerStream OpenAvailable(ref string pipe)
    {
        var names = Candidates(pipe);
        for (var attempt = 0; attempt < names.Count - 1; attempt++)
            try
            {
                pipe = names[attempt];
                return OpenFirst(pipe);
            }
            catch (PipeTakenException)
            {
            }

        pipe = names[^1];
        return OpenFirst(pipe);
    }

    private static NamedPipeServerStream Create(string pipe, bool first)
    {
        var handle = PipeTrust.OpenServer(pipe, first);
        try
        {
            return new NamedPipeServerStream(PipeDirection.InOut, true, false, handle);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private async Task AcceptLoop()
    {
        var waiting = new Dictionary<Task, NamedPipeServerStream>();
        NamedPipeServerStream? first;
        lock (_firstGate)
        {
            first = _first;
            _first = null;
        }

        try
        {
            if (first is null || !StartWaiting(first, waiting))
                return;
            OpenInstances(waiting);
            while (waiting.Count > 0)
            {
                var finished = await Task.WhenAny(waiting.Keys).ConfigureAwait(false);
                waiting.Remove(finished, out var stream);
                if (_cancel.IsCancellationRequested)
                {
                    await stream!.DisposeAsync().ConfigureAwait(false);
                    return;
                }

                OpenInstances(waiting);
                if (finished.IsCompletedSuccessfully && TryTakeClient())
                {
                    var taken = stream!;
                    Track(taken);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Serve(taken).ConfigureAwait(false);
                        }
                        finally
                        {
                            Untrack(taken);
                            ReleaseClient();
                        }
                    });
                }
                else
                {
                    await stream!.DisposeAsync().ConfigureAwait(false);
                }
            }

            _log.Error("ipc", "The control pipe has no instance left, so commands can't reach this shell.");
        }
        finally
        {
            foreach (var stream in waiting.Values)
                await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void OpenInstances(Dictionary<Task, NamedPipeServerStream> waiting)
    {
        var failures = 0;
        while (waiting.Count < WaitingInstances && failures < OpenAttempts)
            try
            {
                if (!StartWaiting(OpenNext(_pipe), waiting))
                    failures++;
            }
            catch (IOException e)
            {
                failures++;
                _log.Warn("ipc", $"Could not open the next control pipe instance: {e.Message}");
            }
    }

    private bool StartWaiting(NamedPipeServerStream stream, Dictionary<Task, NamedPipeServerStream> waiting)
    {
        try
        {
            waiting.Add(stream.WaitForConnectionAsync(_cancel.Token), stream);
            return true;
        }
        catch (IOException e)
        {
            _log.Warn("ipc", $"Could not wait on a control pipe instance: {e.Message}");
            stream.Dispose();
            return false;
        }
    }

    private async Task Serve(NamedPipeServerStream stream)
    {
        try
        {
            await using var _ = stream;
            var refusal = _refusal(stream.SafePipeHandle);
            if (refusal is not null)
                _log.Warn("ipc", $"Refused a control pipe client: {refusal}");
            string? line;
            bool tooLong;
            using (var idle = new CancellationTokenSource(RequestTimeout, _loop.Time))
            using (var reading = CancellationTokenSource.CreateLinkedTokenSource(_cancel.Token, idle.Token))
            {
                (line, tooLong) = await ReadRequest(stream, reading.Token).ConfigureAwait(false);
            }

            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.AutoFlush = true;
            if (tooLong)
            {
                await Send(writer, new IpcResponse(false, Error: "Request is too long."), _cancel.Token)
                    .ConfigureAwait(false);
                return;
            }

            if (line is null)
                return;
            if (refusal is not null)
            {
                await Send(writer, new IpcResponse(false, Error: refusal), _cancel.Token).ConfigureAwait(false);
                return;
            }

            IpcRequest? request;
            try
            {
                request = JsonSerializer.Deserialize(line, IpcJson.Default.IpcRequest);
            }
            catch (JsonException)
            {
                request = null;
            }

            if (request is null)
            {
                await Send(writer, new IpcResponse(false, Error: "Malformed request."), _cancel.Token)
                    .ConfigureAwait(false);
                return;
            }

            if (IpcProtocol.ClientRefusal(request.Protocol) is { } mismatch)
            {
                await Send(writer, new IpcResponse(false, Error: mismatch), _cancel.Token).ConfigureAwait(false);
                return;
            }

            if (IpcOps.ChecksRelease(request.Op) &&
                IpcProtocol.ReleaseRefusal(ProductVersion.Current.Text, request.Winshell) is { } release)
            {
                await Send(writer, new IpcResponse(false, Error: release), _cancel.Token).ConfigureAwait(false);
                return;
            }

            if (request.Op is IpcOps.Listen or IpcOps.Wait)
            {
                await Listen(request, writer, stream, request.Op == IpcOps.Wait).ConfigureAwait(false);
                return;
            }

            var response = await OnUiThread(() => Handle(request)).ConfigureAwait(false);
            try
            {
                await Send(writer, response, _cancel.Token).ConfigureAwait(false);
            }
            finally
            {
                if (request.Op == IpcOps.Reload && response.Ok)
                    _loop.Post(_control.Quit);
            }
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<IpcResponse> Handle(IpcRequest request)
    {
        var ipc = _control.Ipc;
        return request.Op switch
        {
            IpcOps.Show when request.Json => new IpcResponse(true, IpcShowPayload.Write(ipc.Describe())),
            IpcOps.Show => new IpcResponse(true, ipc.Show()),
            IpcOps.Call when request is { Target: "notice", Name: "dismiss" } => DismissNotice(request.Args),
            IpcOps.Call => new IpcResponse(true,
                ipc.Call(request.Target ?? "", request.Name ?? "", request.Args ?? [])),
            IpcOps.Prop => new IpcResponse(true, ipc.GetProperty(request.Target ?? "", request.Name ?? "")),
            IpcOps.Stop => Stop(),
            IpcOps.Yield => Yield(),
            IpcOps.Reload => Reload(request),
            IpcOps.Build => Build(request),
            IpcOps.Inspect => new IpcResponse(true,
                await _control.Inspect(request.Target ?? "", request.Args ?? [])),
            _ => new IpcResponse(false, Error: $"Unknown request {request.Op}.")
        };
    }

    private IpcResponse DismissNotice(List<string>? args)
    {
        if (args is { Count: > 0 })
            return new IpcResponse(false, Error: IpcValues.WrongCount("notice", "dismiss", 0, args.Count));
        _control.DismissNotice();
        return new IpcResponse(true);
    }

    private IpcResponse Stop()
    {
        _loop.Post(_control.Quit);
        return new IpcResponse(true);
    }

    private IpcResponse Yield()
    {
        _control.Yield();
        _loop.Post(_control.Quit);
        return new IpcResponse(true);
    }

    private IpcResponse Reload(IpcRequest request)
    {
        return _control.Reload(request.ReloadProgram) is { } error
            ? new IpcResponse(false, Error: error)
            : new IpcResponse(true);
    }

    private IpcResponse Build(IpcRequest request)
    {
        if (request.BuildReport is not var (error, projectFile))
            return new IpcResponse(false, Error: "build takes the error and the project file.");
        _control.ShowBuildError(error, projectFile);
        return new IpcResponse(true);
    }

    private async Task Listen(IpcRequest request, StreamWriter writer, Stream stream, bool once)
    {
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(MaxQueuedSignals)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        IDisposable? subscription = null;
        var subscribed = await OnUiThread(() =>
        {
            subscription = _control.Ipc.Listen(request.Target ?? "", request.Name ?? "",
                value => channel.Writer.TryWrite(value));
            return Task.FromResult(new IpcResponse(true));
        }).ConfigureAwait(false);
        if (!subscribed.Ok)
        {
            await Send(writer, subscribed, _cancel.Token).ConfigureAwait(false);
            return;
        }

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(_cancel.Token);
        Task<bool>? waiting = null;
        try
        {
            while (true)
            {
                using var pause = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                var gone = WaitForDisconnect(stream, pause.Token);
                waiting = channel.Reader.WaitToReadAsync(stop.Token).AsTask();
                var done = await Task.WhenAny(gone, waiting).ConfigureAwait(false);
                if (gone.IsCompletedSuccessfully || done == gone)
                {
                    await Finish(gone).ConfigureAwait(false);
                    break;
                }

                await pause.CancelAsync().ConfigureAwait(false);
                await Finish(gone).ConfigureAwait(false);
                var signal = waiting;
                waiting = null;
                if (!await signal.ConfigureAwait(false))
                    break;
                while (channel.Reader.TryRead(out var value))
                {
                    await Send(writer, new IpcResponse(true, value), _cancel.Token).ConfigureAwait(false);
                    if (once)
                        return;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            await stop.CancelAsync().ConfigureAwait(false);
            if (waiting is not null)
                await Finish(waiting).ConfigureAwait(false);
            _loop.Post(() => subscription?.Dispose());
        }
    }

    private static async Task<(string? Line, bool TooLong)> ReadRequest(Stream stream, CancellationToken cancel)
    {
        var bytes = ArrayPool<byte>.Shared.Rent(MaxRequestBytes + 1);
        try
        {
            var count = 0;
            while (true)
            {
                var n = await stream.ReadAsync(bytes.AsMemory(count, MaxRequestBytes + 1 - count), cancel)
                    .ConfigureAwait(false);
                if (n == 0)
                    return (null, false);
                var newline = Array.IndexOf(bytes, (byte)'\n', count, n);
                if (newline >= 0)
                {
                    if (newline > 0 && bytes[newline - 1] == (byte)'\r')
                        newline--;
                    return (Encoding.UTF8.GetString(bytes, 0, newline), false);
                }

                count += n;
                if (count > MaxRequestBytes)
                    return (null, true);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    private static async Task WaitForDisconnect(Stream stream, CancellationToken cancel)
    {
        var one = new byte[1];
        while (true)
        {
            var n = await stream.ReadAsync(one.AsMemory(), cancel).ConfigureAwait(false);
            if (n == 0)
                return;
        }
    }

    private static async Task Finish(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception e) when (e is OperationCanceledException or IOException or ObjectDisposedException)
        {
        }
    }

    private bool TryTakeClient()
    {
        lock (_limit)
        {
            if (_clients >= MaxClients)
                return false;
            _clients++;
            return true;
        }
    }

    private void ReleaseClient()
    {
        lock (_limit)
        {
            _clients--;
        }
    }

    private void Track(NamedPipeServerStream stream)
    {
        lock (_limit)
        {
            _serving.Add(stream);
        }
    }

    private void Untrack(NamedPipeServerStream stream)
    {
        lock (_limit)
        {
            _serving.Remove(stream);
        }
    }

    private Task<IpcResponse> OnUiThread(Func<Task<IpcResponse>> work)
    {
        var tcs = new TaskCompletionSource<IpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _loop.Post(() => _ = Run());
        return tcs.Task;

        async Task Run()
        {
            try
            {
                tcs.SetResult(await work());
            }
            catch (IpcException e)
            {
                tcs.SetResult(new IpcResponse(false, Error: e.Message));
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                tcs.SetResult(new IpcResponse(false, Error: $"ipc: {e.GetType().Name}: {e.Message}"));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
        }
    }

    private static Task Send(StreamWriter writer, IpcResponse response, CancellationToken cancel)
    {
        return writer.WriteLineAsync(
            JsonSerializer
                .Serialize(response with { Protocol = IpcProtocol.Current, Winshell = ProductVersion.Current.Text },
                    IpcJson.Default.IpcResponse).AsMemory(), cancel);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _cancel.Cancel();
        NamedPipeServerStream[] open;
        lock (_limit)
        {
            open = [.. _serving];
        }

        foreach (var stream in open)
            try
            {
                stream.Dispose();
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
            }

        lock (_firstGate)
        {
            _first?.Dispose();
            _first = null;
        }
    }
}
