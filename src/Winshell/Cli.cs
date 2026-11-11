using System.Text;
using Winshell.Commands;

namespace Winshell;

internal static class Cli
{
    public static async Task<int> RunAsync(string[] args)
    {
        WriteUtf8WhenRedirected();
        Invocation parsed;
        try
        {
            parsed = CliParser.Parse(CommandLine.Parse(args));
        }
        catch (ArgumentException e)
        {
            await Console.Error.WriteLineAsync(e.Message);
            return 2;
        }

        var command = parsed.Command;
        if (parsed.ShowHelp)
        {
            Console.WriteLine(CliParser.Help(command));
            return 0;
        }

        if (parsed.ShowVersion)
        {
            Console.WriteLine($"winshell {Shell.Version}");
            return 0;
        }

        try
        {
            if (command is null)
            {
                // The Start tile and sign-in host still start a shell without a command word.
                var windowless = WinshellHost.IsWindowless(Environment.ProcessPath);
                if (parsed.Arguments.IsEmpty && !windowless)
                {
                    Console.WriteLine(CliParser.Help(null));
                    return 0;
                }

                if (!windowless && !parsed.ShellStartAllowed)
                {
                    await Console.Error.WriteLineAsync("No command. Use winshell run to start a shell.");
                    return 2;
                }

                return RunCommand.Execute(parsed);
            }

            return await Dispatch(parsed);
        }
        catch (Exception e) when (e is not OutOfMemoryException && command is not (null or CommandWords.Run))
        {
            await Console.Error.WriteLineAsync(parsed.Verbose
                ? $"winshell {command}: {e}"
                : $"winshell {command}: {e.Message}");
            return 1;
        }
    }

    private static Task<int> Dispatch(Invocation parsed)
    {
        var entry = CliParser.Entries.First(candidate => candidate.Name == parsed.Command);
        if (entry.Words == WordRule.Subcommand)
        {
            var sub = entry.Subcommands.First(candidate => candidate.Name == parsed.Positional[0]);
            if (sub.Run is not null)
                return sub.Run(parsed);
        }

        return entry.Run!(parsed);
    }

    /// <summary>
    /// A pipe or a file gets UTF-8. The console's own code page would turn text outside it into question marks,
    /// and setting that code page would change it for the terminal too.
    /// </summary>
    private static void WriteUtf8WhenRedirected()
    {
        var utf8 = new UTF8Encoding(false);
        if (Console.IsOutputRedirected)
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true });
        if (Console.IsErrorRedirected)
            Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true });
    }
}
