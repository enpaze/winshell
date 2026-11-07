using System.Globalization;
using System.Text;

namespace Winshell.Commands;

internal static class NewCommand
{
    private const string Guide = "https://github.com/enpaze/winshell/blob/main/docs/configuration.md#from-a-clone";

    /// <summary>winshell new: lists what it can copy, or copies one into the config folder or <c>--path</c>.</summary>
    internal static int Run(CliOptions options)
    {
        var packages = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "packages");
        var result = Make(options, ShellLocations.Current.ConfigRoot, packages);
        Console.Write(result.Output);
        if (result.Error is not null)
            Console.Error.WriteLine(result.Error);
        return result.Code;
    }

    /// <summary>
    /// What <see cref="Run"/> prints and returns. <paramref name="packages"/> is the folder beside winshell.exe that
    /// may hold this release's Winshell package. An unknown name, or a folder that isn't empty, changes nothing.
    /// </summary>
    internal static Result Make(CliOptions options, string configRoot, string packages)
    {
        var output = new StringBuilder();
        if (options.Positional.FirstOrDefault() is not { } name)
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
        var folder = Path.GetFullPath(options.Path ?? Path.Combine(configRoot, chosen.Name));
        if (File.Exists(folder) || (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any()))
            return new Result(2, "", $"{folder} already exists and isn't empty. Pick another folder with -p.");

        var version = PackageVersion(Shell.Version);
        var restores = chosen.CopyTo(folder, version, packages);
        output.AppendLine(CultureInfo.InvariantCulture, $"Created {folder}");
        if (!restores)
            output.AppendLine(CultureInfo.InvariantCulture,
                $"The Winshell {version} package is not beside winshell.exe, so add a NuGet source for it: see {Guide}");
        var run = options.Path is null
            ? "-c " + chosen.Name
            : "-p " + (folder.Contains(' ', StringComparison.Ordinal) ? $"\"{folder}\"" : folder);
        output.AppendLine(CultureInfo.InvariantCulture, $"Run it: winshell run {run}");
        return new Result(0, output.ToString(), null);
    }

    /// <summary>The release a package is named after: the version without its build id, such as <c>0.6.0</c>.</summary>
    private static string PackageVersion(string version)
    {
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? version : version[..plus];
    }

    internal sealed record Result(int Code, string Output, string? Error);
}
