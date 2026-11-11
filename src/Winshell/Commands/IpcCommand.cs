using System.Diagnostics;

namespace Winshell.Commands;

internal static class IpcCommand
{
    internal static async Task<int> RunAsync(CliOptions options)
    {
        var args = options.Positional;
        var request = args[0] switch
        {
            IpcOps.Show => IpcRequest.ForShow(options.Json),
            IpcOps.Call => IpcRequest.ForCall(args[1], args[2], args.Skip(3).ToList()),
            IpcOps.Prop => IpcRequest.ForProp(args[2], args[3]),
            IpcOps.Wait => IpcRequest.ForWait(args[1], args[2]),
            IpcOps.Listen => IpcRequest.ForListen(args[1], args[2]),
            _ => throw new UnreachableException($"No ipc command \"{args[0]}\".")
        };
        if (InstanceCall.Select(options, InstanceSelection.Question.Running, out var code) is not { } info)
            return code;
        if (request.Op == IpcOps.Show && request.Json)
            return await PrintShowJson(info, request);
        if (request.Op != IpcOps.Listen)
            return await InstanceCall.PrintAsync(info, request);

        // A listen prints each signal, and ends without a value when the shell quits.
        await foreach (var reply in IpcClient.Request(info.Pipe, request))
            if (InstanceCall.Print(InstanceCall.Answer.Of(reply, info.Id)) != 0)
                return 1;

        return 0;
    }

    /// <summary>The pipe carries the target list. This prints it the way <c>list --json</c> prints.</summary>
    private static async Task<int> PrintShowJson(InstanceInfo info, IpcRequest request)
    {
        var answer = await InstanceCall.AskAsync(info, request);
        if (answer.Response is not { Ok: true, Value: { } value })
            return InstanceCall.Print(answer);
        Console.WriteLine(CliJson.IpcShow(value));
        return 0;
    }
}
