namespace Winshell;

/// <summary>The build-error notice a reload or a dev session asks a running instance to show.</summary>
internal static class BuildNotice
{
    internal static async Task Show(InstanceInfo info, string error, string projectFile)
    {
        var answer = await InstanceCall.AskAsync(info, IpcRequest.ForBuild(error, projectFile));
        if (answer.Problem($"Instance {info.Id} could not show the notice") is { } problem)
            await Console.Error.WriteLineAsync(problem);
    }
}
