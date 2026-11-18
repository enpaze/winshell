namespace Winshell;

internal static class InstanceCall
{
    internal static InstanceInfo? Select(InstanceChoice choice, InstanceSelection.Question question, out int code)
    {
        var selected = InstanceSelection.Select(choice, question, ShellLocations.Current.Instances.All());
        code = selected.Code;
        if (selected.Info is null)
            Console.Error.WriteLine(selected.Error);
        else if (selected.Notice is not null)
            Console.Error.WriteLine(selected.Notice);
        return selected.Info;
    }

    internal static async Task<Answer> AskAsync(InstanceInfo info, IpcRequest request,
        CancellationToken cancel = default)
    {
        await foreach (var reply in IpcClient.Request(info.Pipe, request, cancel))
            return Answer.Of(reply, info.Id);

        return new Answer(null, $"Instance {info.Id} closed the control pipe without answering.");
    }

    internal static async Task<int> PrintAsync(InstanceInfo info, IpcRequest request)
    {
        return Print(await AskAsync(info, request));
    }

    internal static int Print(Answer answer)
    {
        if (answer.Response is not { Ok: true } response)
        {
            Console.Error.WriteLine(answer.Problem(null));
            return 1;
        }

        if (!string.IsNullOrEmpty(response.Value))
            Console.WriteLine(response.Value.TrimEnd('\n'));
        return 0;
    }

    internal readonly record struct Answer(IpcResponse? Response, string? Failure)
    {
        public static Answer Of(IpcReply reply, string instanceId)
        {
            return reply switch
            {
                IpcFailure failure => new Answer(null, failure.Sentence(instanceId)),
                IpcResponse response => new Answer(response, null),
            };
        }

        public string? Problem(string? refused)
        {
            if (Response is { Ok: false } response)
                return refused is null ? response.Error : $"{refused}: {response.Error}";
            return Failure;
        }
    }
}
