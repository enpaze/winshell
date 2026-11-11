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

    internal static int Execute(Invocation parsed)
    {
        var instances = ShellLocations.Current.Instances.List(false);
        if (parsed.IsSet(CliFlags.Json))
        {
            Console.WriteLine(CliJson.Instances(instances.Select(i => i.Info)));
            return 0;
        }

        if (instances.Count == 0)
        {
            Console.WriteLine(InstanceSelection.NoneRunning);
            return 0;
        }

        foreach (var (info, _) in instances)
        {
            Console.WriteLine($"Instance {info.Id}:");
            Console.WriteLine($"  Process ID: {info.Pid}");
            Console.WriteLine($"  Shell ID: {info.ShellId}");
            Console.WriteLine($"  Config path: {info.ConfigPath}");
            Console.WriteLine($"  Launch time: {info.LaunchTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  Log file: {info.Log}");
        }

        return 0;
    }
}
