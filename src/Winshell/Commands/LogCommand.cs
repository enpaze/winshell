namespace Winshell.Commands;

internal static class LogCommand
{
    internal static readonly CommandEntry Entry = new()
    {
        Name = "log",
        Overview = "       winshell log [-f] [-t N] [file]     print an instance's log",
        Usage = """
                winshell log [-i <id> | --pid <pid> | -c <name> | -p <path>] [-t <n>] [-f] [file]
                  Prints an instance's log. -t, --tail <n> prints the last n lines.
                  -f, --follow keeps printing new lines. winshell log <file> prints that file.
                  With one running instance and no selector, that instance is used.
                  With several, this exits and lists them.
                  With nothing running and no selector, the log of a stopped instance is printed.
                """,
        Options = [.. CliFlags.Instance, CliFlags.Tail, CliFlags.Follow],
        Words = WordRule.One,
        Run = Execute
    };

    internal static async Task<int> Execute(Invocation parsed)
    {
        var path = parsed.Positional.FirstOrDefault();
        if (path is null)
        {
            if (InstanceCall.Select(parsed.Instance, InstanceSelection.Question.Log, out var code) is not { } info)
                return code;
            path = info.Log;
        }

        if (!File.Exists(path))
        {
            await Console.Error.WriteLineAsync($"No log at {path}.");
            return 1;
        }

        await using var stream = InstanceLog.OpenToRead(path);
        using var reader = new StreamReader(stream);
        var all = (await reader.ReadToEndAsync()).TrimEnd('\n').Split('\n');
        foreach (var line in parsed.Number(CliFlags.Tail) is { } n ? all.TakeLast(n) : all)
            if (line.Length > 0)
                Console.WriteLine(line.TrimEnd('\r'));
        if (parsed.IsSet(CliFlags.Follow))
            await foreach (var line in InstanceLog.Follow(reader))
                Console.WriteLine(line);

        return 0;
    }
}
