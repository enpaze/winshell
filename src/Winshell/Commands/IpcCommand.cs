namespace Winshell.Commands;

internal static class IpcCommand
{
    internal static readonly CommandEntry Entry = new()
    {
        Name = "ipc",
        Overview = """
                          winshell ipc show [-j]              list IPC targets
                          winshell ipc call <target> <function> [args...]
                          winshell ipc get <target> <property>
                          winshell ipc wait <target> <signal>
                          winshell ipc listen <target> <signal>
                   """,
        Usage = """
                winshell ipc [-i <id> | --pid <pid> | -s <name> | -p <path>] <command>
                  show [-j]                             list IPC targets. -j prints JSON
                  call <target> <function> [args...]    call a function and print the result
                  call notice dismiss                   close Winshell's own notice
                  get <target> <property>               print a property
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
            new CommandEntry { Name = "get", MinWords = 2, Missing = "a target and a property", Run = Get },
            new CommandEntry { Name = IpcOps.Wait, MinWords = 2, Missing = "a target and a signal", Run = Wait },
            new CommandEntry { Name = IpcOps.Listen, MinWords = 2, Missing = "a target and a signal", Run = Listen }
        ]
    };

    private static async Task<int> Show(Invocation parsed)
    {
        if (InstanceCall.Select(parsed.Instance, InstanceSelection.Question.Running, out var code) is not { } info)
            return code;
        var json = parsed.IsSet(CliFlags.Json);
        var answer = await InstanceCall.AskAsync(info, IpcRequest.ForShow(json));
        if (!json || answer.Response is not { Ok: true, Value: { } value })
            return InstanceCall.Print(answer);
        Console.WriteLine(CliJson.IpcShow(value));
        return 0;
    }

    private static Task<int> Call(Invocation parsed)
    {
        var args = parsed.Positional;
        return Send(parsed, IpcRequest.ForCall(args[1], args[2], args.Skip(3).ToList()));
    }

    private static Task<int> Get(Invocation parsed)
    {
        var args = parsed.Positional;
        return Send(parsed, IpcRequest.ForProp(args[1], args[2]));
    }

    private static Task<int> Wait(Invocation parsed)
    {
        var args = parsed.Positional;
        return Send(parsed, IpcRequest.ForWait(args[1], args[2]));
    }

    private static async Task<int> Listen(Invocation parsed)
    {
        if (InstanceCall.Select(parsed.Instance, InstanceSelection.Question.Running, out var code) is not { } info)
            return code;
        var args = parsed.Positional;
        await foreach (var reply in IpcClient.Request(info.Pipe, IpcRequest.ForListen(args[1], args[2])))
            if (InstanceCall.Print(InstanceCall.Answer.Of(reply, info.Id)) != 0)
                return 1;

        return 0;
    }

    private static async Task<int> Send(Invocation parsed, IpcRequest request)
    {
        if (InstanceCall.Select(parsed.Instance, InstanceSelection.Question.Running, out var code) is not { } info)
            return code;
        return await InstanceCall.PrintAsync(info, request);
    }
}
