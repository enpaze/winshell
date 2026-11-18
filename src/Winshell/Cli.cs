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
            parsed = CliParser.Parse(args);
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

            return await parsed.Run!(parsed);
        }
        catch (Exception e) when (e is not OutOfMemoryException && command is not (null or CommandWords.Run))
        {
            await Console.Error.WriteLineAsync(parsed.Verbose
                ? $"winshell {command}: {e}"
                : $"winshell {command}: {e.Message}");
            return 1;
        }
    }

    private static void WriteUtf8WhenRedirected()
    {
        var utf8 = new UTF8Encoding(false);
        if (Console.IsOutputRedirected)
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true });
        if (Console.IsErrorRedirected)
            Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true });
    }
}
