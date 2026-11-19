namespace Winshell.Commands;

internal static class StopCommand
{
    internal static readonly CommandEntry Entry = new()
    {
        Name = "stop",
        Overview = """
                          winshell stop [--force] [instance options]
                                                              stop an instance and wait until it exits
                   """,
        Usage = """
                winshell stop [--force] [-i <id> | --pid <pid> | -s <name> | -p <path>]
                  Asks one running instance to exit, then waits until that process has exited.
                  --force ends the process when it does not exit.
                  With one running instance and no selector, that instance is used.
                  With several, this exits and lists them.
                """,
        Options = [.. CliFlags.Instance, CliFlags.Force],
        Run = Execute
    };

    private static async Task<int> Execute(Invocation parsed)
    {
        if (InstanceCall.Select(parsed.Instance, InstanceSelection.Question.Stop, out var code) is not { } info)
            return code;

        var force = parsed.IsSet(CliFlags.Force);
        if (!await StopAsync(info, force))
        {
            await Console.Error.WriteLineAsync(force
                ? $"Could not end instance {info.Id}."
                : $"Instance {info.Id} is still running. Use --force to end it.");
            return 1;
        }

        Console.WriteLine($"Stopped {info.Id}");
        return 0;
    }

    internal static async Task<bool> StopAsync(InstanceInfo info, bool force)
    {
        var quit = await ShellExit.QuitAsync(info, ExitIntent.Stop, QuitPatience.Interactive,
            force ? ShellExit.Ending.Always : ShellExit.Ending.Never);
        if (!quit.Exited && quit.Answer.Problem($"Instance {info.Id} refused") is { } problem)
            await Console.Error.WriteLineAsync(problem);
        return quit.Exited;
    }
}
