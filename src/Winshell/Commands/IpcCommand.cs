namespace Winshell.Commands;

internal static class IpcCommand
{
    internal static readonly CommandEntry Entry = new()
    {
        Name = "ipc",
        Overview = """
                          winshell ipc show [-j]              list IPC targets
                          winshell ipc call <target> <function> [args...]
                          winshell ipc prop get <target> <property>
                          winshell ipc wait <target> <signal>
                          winshell ipc listen <target> <signal>
                   """,
        Usage = """
                winshell ipc [-i <id> | --pid <pid> | -c <name> | -p <path>] <command>
                  show [-j]                             list IPC targets. -j prints JSON
                  call <target> <function> [args...]    call a function and print the result
                  call notice dismiss                   close Winshell's own notice
                  prop get <target> <property>          print a property
                  wait <target> <signal>                wait for one signal
                  listen <target> <signal>              print each signal until Ctrl+C
                  Words after the function name are arguments, even when they start with -.
                  A -- ends options on every command.
                """,
        Options = [.. CliFlags.Instance, CliFlags.Json],
        Words = WordRule.Subcommand,
        Subcommands =
        [
            new CommandEntry { Name = IpcOps.Show, Run = Show },
            new CommandEntry
            {
                Name = IpcOps.Call,
                MinWords = 2,
                Missing = "a target and a function",
                RawAfter = true,
                Run = Call
            },
            new CommandEntry
            {
                Name = IpcOps.Prop,
                MinWords = 3,
                FirstWord = "get",
                Missing = "\"get\", a target and a property",
                Run = Prop
            },
            new CommandEntry { Name = IpcOps.Wait, MinWords = 2, Missing = "a target and a signal", Run = Wait },
            new CommandEntry { Name = IpcOps.Listen, MinWords = 2, Missing = "a target and a signal", Run = Listen }
        ]
    };

    private static Task<int> Show(Invocation parsed)
    {
        return Send(parsed, IpcRequest.ForShow(parsed.IsSet(CliFlags.Json)));
    }

    private static Task<int> Call(Invocation parsed)
    {
        var args = parsed.Positional;
        return Send(parsed, IpcRequest.ForCall(args[1], args[2], args.Skip(3).ToList()));
    }

    private static Task<int> Prop(Invocation parsed)
    {
        var args = parsed.Positional;
        return Send(parsed, IpcRequest.ForProp(args[2], args[3]));
    }

    private static Task<int> Wait(Invocation parsed)
    {
        var args = parsed.Positional;
        return Send(parsed, IpcRequest.ForWait(args[1], args[2]));
    }

    private static Task<int> Listen(Invocation parsed)
    {
        var args = parsed.Positional;
        return Send(parsed, IpcRequest.ForListen(args[1], args[2]));
    }

    private static async Task<int> Send(Invocation parsed, IpcRequest request)
    {
        if (InstanceCall.Select(parsed.Instance, InstanceSelection.Question.Running, out var code) is not { } info)
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
