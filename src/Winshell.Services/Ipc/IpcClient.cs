using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Winshell;

internal readonly record struct IpcFailure(string Reason, bool Rejected)
{
    public string Sentence(string instanceId)
    {
        return Rejected
            ? $"Rejected instance {instanceId}: {Reason}"
            : $"Could not reach instance {instanceId}: {Reason}";
    }
}

internal union IpcReply(IpcResponse, IpcFailure);

internal static class IpcClient
{
    internal static readonly TimeSpan ConnectLimit = TimeSpan.FromSeconds(3);

    public static async IAsyncEnumerable<IpcReply> Request(string pipe, IpcRequest request,
        [EnumeratorCancellation] CancellationToken cancel = default)
    {
        var connected = await Connect(pipe, cancel).ConfigureAwait(false);
        if (connected.Failure is { } connectFailure)
        {
            yield return connectFailure;
            yield break;
        }

        await using var client = connected.Client!;
        if (PipeTrust.ServerRefusal(client) is { } refusal)
        {
            yield return new IpcFailure(refusal, true);
            yield break;
        }

        using var reader = new StreamReader(client, Encoding.UTF8);
        var sent = JsonSerializer.Serialize(
            request with { Protocol = IpcProtocol.Current, Winshell = ProductVersion.Current.Text },
            IpcJson.Default.IpcRequest);
        if (await Write(client, sent, cancel).ConfigureAwait(false) is { } writeFailure)
        {
            yield return writeFailure;
            yield break;
        }

        while (true)
        {
            var read = await Read(reader, cancel).ConfigureAwait(false);
            if (read.Failure is { } readFailure)
            {
                yield return readFailure;
                yield break;
            }

            if (read.Line is null)
                yield break;
            var response = JsonSerializer.Deserialize(read.Line, IpcJson.Default.IpcResponse);
            if (response is null)
                continue;
            if (response.Ok && IpcProtocol.ShellRefusal(response.Protocol) is { } mismatch)
            {
                yield return new IpcFailure(mismatch, true);
                yield break;
            }

            if (response.Ok && IpcOps.ChecksRelease(request.Op) &&
                IpcProtocol.ReleaseRefusal(response.Winshell, ProductVersion.Current.Text) is { } release)
            {
                yield return new IpcFailure(release, true);
                yield break;
            }

            yield return response;
        }
    }

    private readonly record struct Connection(NamedPipeClientStream? Client, IpcFailure? Failure);

    private static async Task<Connection> Connect(string pipe, CancellationToken cancel)
    {
        var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
        var connected = false;
        try
        {
            await client.ConnectAsync(ConnectLimit, cancel).ConfigureAwait(false);
            connected = true;
            return new Connection(client, null);
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return new Connection(null, new IpcFailure(e.Message, false));
        }
        finally
        {
            if (!connected)
                await client.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<IpcFailure?> Write(NamedPipeClientStream client, string json, CancellationToken cancel)
    {
        try
        {
            await client.WriteAsync(Encoding.UTF8.GetBytes(json + Environment.NewLine), cancel).ConfigureAwait(false);
            return null;
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return new IpcFailure(e.Message, false);
        }
    }

    private readonly record struct ReadLine(string? Line, IpcFailure? Failure);

    private static async Task<ReadLine> Read(StreamReader reader, CancellationToken cancel)
    {
        try
        {
            return new ReadLine(await reader.ReadLineAsync(cancel).ConfigureAwait(false), null);
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return new ReadLine(null, new IpcFailure(e.Message, false));
        }
    }
}
