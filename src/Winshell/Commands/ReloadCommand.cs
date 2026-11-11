namespace Winshell.Commands;

internal static class ReloadCommand
{
    internal static readonly CommandEntry Entry = new()
    {
        Name = "reload",
        Overview = """
                          winshell reload [instance options]
                                                              reload an instance's configuration
                   """,
        Usage = """
                winshell reload [-i <id> | --pid <pid> | -c <name> | -p <path>]
                  Builds a configuration project and asks its instance to start again.
                  A build that fails leaves the instance running and asks it to show the error.
                  With one running instance and no selector, that instance is used.
                  With several, this exits and lists them.
                """,
        Options = CliFlags.Instance,
        Run = Execute
    };

    internal static async Task<int> Execute(Invocation parsed)
    {
        if (InstanceCall.Select(parsed.Instance, InstanceSelection.Question.Running, out var code) is not { } info)
            return code;

        var project = ConfigurationProject.ForProgram(info.ConfigPath);
        if (project is not null)
            if (project.Prepare(false, ConfigurationProject.Run) is { } reason)
            {
                await Console.Error.WriteLineAsync(reason);
                await BuildNotice.Show(info, reason, project.ProjectFile);
                return 1;
            }

        if (ShellExit.IsSupervised(info.Pid))
        {
            // Its supervisor starts the last build and owns it, so the shell only quits. Without a project there is none.
            if (project is null)
            {
                await Console.Error.WriteLineAsync(
                    $"Could not find the project that builds instance {info.Id}, so it was not reloaded.");
                return 1;
            }

            // One that accepted the quit and hangs on its way out is ended, as a reload asked for the new one.
            var quit = await ShellExit.QuitAsync(info, ExitIntent.Reload, ShellExit.Patience.Interactive,
                ShellExit.Ending.IfAccepted);
            if (!quit.Exited)
            {
                await Console.Error.WriteLineAsync(Failure(info, quit.Answer) ?? $"Could not end instance {info.Id}.");
                return 1;
            }

            Console.WriteLine($"Reloaded {info.Id}");
            return 0;
        }

        // Any other shell hands over to a process it starts itself. A handover starts the program the build left, which is
        // not always the one this instance runs.
        var answer = await InstanceCall.AskAsync(info, IpcRequest.ForReload(project?.Program));
        if (Failure(info, answer) is { } refusal)
        {
            await Console.Error.WriteLineAsync(refusal);
            return 1;
        }

        Console.WriteLine($"Reloaded {info.Id}");
        return 0;
    }

    private static string? Failure(InstanceInfo info, InstanceCall.Answer answer)
    {
        return answer.Problem($"Instance {info.Id} did not reload");
    }
}
