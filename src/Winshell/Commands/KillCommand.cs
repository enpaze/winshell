namespace Winshell.Commands;

internal static class KillCommand
{
    internal static readonly CommandEntry Entry = new()
    {
        Name = "kill",
        Overview = """
                          winshell kill [--force] [instance options]
                                                              stop an instance and wait until it exits
                   """,
        Usage = """
                winshell kill [--force] [-i <id> | --pid <pid> | -c <name> | -p <path>]
                  Asks one running instance to exit, then waits until that process has exited.
                  --force ends the process when it does not exit.
                  With one running instance and no selector, that instance is used.
                  With several, this exits and lists them.
                """,
        Options = [.. CliFlags.Instance, CliFlags.Force],
        Run = Execute
    };

    internal static async Task<int> Execute(Invocation parsed)
    {
        if (InstanceCall.Select(parsed.Instance, InstanceSelection.Question.Kill, out var code) is not { } info)
            return code;

        var force = parsed.IsSet(CliFlags.Force);
        // Its supervisor must not start it again, even after a crash on the way out or the end --force gives it.
        var quit = await ShellExit.QuitAsync(info, ExitIntent.Stop, ShellExit.Patience.Interactive,
            force ? ShellExit.Ending.Always : ShellExit.Ending.Never);
        if (!quit.Exited)
        {
            if (quit.Answer.Problem($"Instance {info.Id} refused") is { } problem)
                await Console.Error.WriteLineAsync(problem);
            await Console.Error.WriteLineAsync(force
                ? $"Could not end instance {info.Id}."
                : $"Instance {info.Id} is still running. Use --force to end it.");
            return 1;
        }

        Console.WriteLine($"Killed {info.Id}");
        return 0;
    }
}
