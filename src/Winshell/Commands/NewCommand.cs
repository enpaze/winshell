using System.Globalization;
using System.Text;

namespace Winshell.Commands;

internal static class NewCommand
{
    private const string Guide = "https://github.com/enpaze/winshell/blob/main/docs/configuration.md#from-a-clone";

    internal static readonly CommandEntry Entry = new()
    {
        Name = "new",
        Overview = "       winshell new [<name>] [-p <folder>] list the examples, or copy one to start from",
        Usage = """
                winshell new [<name>] [-p <folder>]
                  With no name, lists the starter and the examples this release can copy.
                  With a name, copies that one into {shells-root}\<name>, ready for
                  winshell run -s <name>. Its project references this release's Winshell package.
                  -p, --path <folder>    copy into this folder instead
                  A folder that exists and isn't empty is refused, and nothing changes.
                """,
        Options = [CliFlags.Path],
        Words = WordRule.One,
        Run = static parsed => Task.FromResult(Execute(parsed))
    };

    private static int Execute(Invocation parsed)
    {
        var packages = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "packages");
        var result = Make(parsed, ShellLocations.Current.ShellsRoot, packages);
        Console.Write(result.Output);
        if (result.Error is not null)
            Console.Error.WriteLine(result.Error);
        return result.Code;
    }

    internal static Result Make(Invocation parsed, string shellsRoot, string packages)
    {
        var output = new StringBuilder();
        if (parsed.Positional.FirstOrDefault() is not { } name)
        {
            var width = Example.All.Max(example => example.Name.Length);
            foreach (var example in Example.All)
                output.AppendLine($"  {example.Name.PadRight(width)}   {example.Description}".TrimEnd());
            return new Result(0, output.ToString(), null);
        }

        var chosen =
            Example.All.FirstOrDefault(example => example.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (chosen is null)
            return new Result(2, "", $"No starter or example is named \"{name}\". Run winshell new to list them.");
        var path = parsed.Text(CliFlags.Path);
        var folder = Path.GetFullPath(path ?? Path.Combine(shellsRoot, chosen.Name));
        if (File.Exists(folder) || (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any()))
            return new Result(2, "", $"{folder} already exists and isn't empty. Pick another folder with -p.");

        var version = PackageVersion(Shell.Version);
        var restores = chosen.CopyTo(folder, version, packages);
        output.AppendLine(CultureInfo.InvariantCulture, $"Created {folder}");
        if (!restores)
            output.AppendLine(CultureInfo.InvariantCulture,
                $"The Winshell {version} package is not beside winshell.exe, so add a NuGet source for it: see {Guide}");
        var run = path is null
            ? $"{CommandWords.Shell.Short} {chosen.Name}"
            : "-p " + (folder.Contains(' ', StringComparison.Ordinal) ? $"\"{folder}\"" : folder);
        output.AppendLine(CultureInfo.InvariantCulture, $"Run it: winshell run {run}");
        return new Result(0, output.ToString(), null);
    }

    private static string PackageVersion(string version)
    {
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? version : version[..plus];
    }

    internal sealed record Result(int Code, string Output, string? Error);
}
