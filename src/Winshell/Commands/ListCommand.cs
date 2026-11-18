namespace Winshell.Commands;

internal static class ListCommand
{
    internal static readonly CommandEntry Entry = new()
    {
        Name = "list",
        Overview = "       winshell list [-j]                  list running instances",
        Usage = """
                winshell list [-j]
                  Lists running instances. -j, --json prints them as JSON.
                  Stopped instances are not listed.
                """,
        Options = [CliFlags.Json],
        Run = static parsed => Task.FromResult(Execute(parsed))
    };

    private static int Execute(Invocation parsed)
    {
        var instances = ShellLocations.Current.Instances.Running();
        if (parsed.IsSet(CliFlags.Json))
        {
            Console.WriteLine(CliJson.Instances(instances));
            return 0;
        }

        if (instances.Count == 0)
        {
            Console.WriteLine(InstanceSelection.NoneRunning);
            return 0;
        }

        foreach (var info in instances)
        {
            Console.WriteLine($"Instance {info.Id}:");
            Console.WriteLine($"  Process ID: {info.Pid}");
            Console.WriteLine($"  Shell ID: {info.ShellId}");
            Console.WriteLine($"  Config path: {info.Program}");
            Console.WriteLine($"  Launch time: {info.LaunchTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  Log file: {info.Log}");
        }

        return 0;
    }
}
