using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Winshell;

internal sealed record CommandResult(int ExitCode, string Output);

internal delegate CommandResult RunProcess(string file, IReadOnlyList<string> arguments, string folder);

internal enum ShellProjectBuild
{
    Run,

    Dev
}

internal sealed partial class ShellProject
{
    internal const int MostErrors = 10;

    internal const string NeedsSdk = "Building a shell needs the .NET SDK, from https://dot.net/download.";

    private static readonly TimeSpan BuildLimit = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StreamLimit = TimeSpan.FromSeconds(1);

    private ShellProject(string projectFile)
    {
        ProjectFile = projectFile;
        Folder = Path.GetDirectoryName(projectFile)!;
        Name = Path.GetFileNameWithoutExtension(projectFile);
    }

    public string ProjectFile { get; }

    public string Folder { get; }

    public string Name { get; }

    public string OutputFolder => Path.Combine(Folder, "bin", Output);

    private string BesideFolder => Path.Combine(Folder, "bin", OutputBeside);

    private const string Output = "shell";
    private const string OutputBeside = "shell.next";

    public static string? FolderOf(string program)
    {
        if (Full(program) is not { } full
            || Path.GetDirectoryName(full) is not { } output
            || Path.GetDirectoryName(output) is not { } bin
            || Path.GetDirectoryName(bin) is not { } folder
            || !Path.GetFileName(bin).Equals("bin", StringComparison.OrdinalIgnoreCase))
            return null;
        var name = Path.GetFileName(output);
        return name.Equals(Output, StringComparison.OrdinalIgnoreCase) ||
               name.Equals(OutputBeside, StringComparison.OrdinalIgnoreCase)
            ? folder
            : null;
    }

    public static string? InOutputFolder(string program)
    {
        return FolderOf(program) is { } folder ? Path.Combine(folder, "bin", Output, Path.GetFileName(program)) : null;
    }

    public static ShellProject? ForProgram(string program)
    {
        if (FolderOf(program) is not { } folder)
            return null;
        var full = Path.GetFullPath(program);
        return Projects(folder).Select(file => new ShellProject(file))
            .FirstOrDefault(project => project.IsProgram(full));
    }

    public static string? RunPath(string program)
    {
        if (FolderOf(program) is not { } folder)
            return null;
        try
        {
            return Projects(folder).Length > 1 && ForProgram(program) is { } project ? project.ProjectFile : folder;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return folder;
        }
    }

    public IEnumerable<InstanceInfo> InstancesOf(ShellProjectBuild build, IEnumerable<InstanceInfo> instances)
    {
        return instances.Where(instance => BuildOf(instance.Program) == build);
    }

    private ShellProjectBuild? BuildOf(string program)
    {
        if (Full(program) is not { } full || !full.StartsWith(Path.GetFullPath(Folder) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            return null;
        return IsProgram(full) ? ShellProjectBuild.Run : ShellProjectBuild.Dev;
    }

    private bool IsProgram(string full)
    {
        var folder = Path.GetDirectoryName(full);
        return (SameFolder(folder, OutputFolder) || SameFolder(folder, BesideFolder)) &&
               Path.GetFileName(full).Equals(AssemblyName() + ".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameFolder(string? full, string folder)
    {
        return full is not null && full.Equals(Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase);
    }

    private static string? Full(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException
                                      or IOException)
        {
            return null;
        }
    }

    public string Program
    {
        get
        {
            var program = ProgramIn(OutputFolder);
            var beside = ProgramIn(BesideFolder);
            return File.Exists(beside) && (!File.Exists(program) ||
                                           File.GetLastWriteTimeUtc(beside) > File.GetLastWriteTimeUtc(program))
                ? beside
                : program;
        }
    }

    private string ProgramIn(string folder)
    {
        return Path.Combine(folder, AssemblyName() + ".exe");
    }

    private const string ChoiceFile = "chosen.txt";

    public static string? Chosen(string root)
    {
        var file = Path.Combine(root, ChoiceFile);
        var name = File.Exists(file) ? File.ReadAllText(file).Trim() : "";
        return name.Length > 0 ? name : null;
    }

    public static void Record(string root, string name)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, ChoiceFile), name + Environment.NewLine);
    }

    public static IReadOnlyList<string> Names(string root)
    {
        return Directory.Exists(root)
            ? Directory.EnumerateDirectories(root).Where(folder => Projects(folder).Length > 0)
                .Select(folder => Path.GetFileName(folder)).ToList()
            : [];
    }

    public static ShellProject? Find(string? path, string? name, string root, out string? error)
    {
        try
        {
            return Choose(path, name, root, out error);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            var what = path ?? (name is not null ? $"shell \"{name}\"" : "the shells folder");
            error = $"Winshell cannot read {what}: {e.Message}";
            return null;
        }
    }

    private static ShellProject? Choose(string? path, string? name, string root, out string? error)
    {
        error = null;
        if (path is not null)
        {
            var full = Path.GetFullPath(path);
            if (File.Exists(full))
            {
                if (full.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                    return new ShellProject(full);
                error = $"No .csproj at {full}.";
                return null;
            }

            var found = In(full, out error);
            if (found is null && error is null)
                error = $"No .csproj at {full}.";
            return found;
        }

        if (name is not null)
        {
            var folder = Path.Combine(root, name);
            var found = In(folder, out error);
            if (found is null && error is null)
                error = $"No shell named \"{name}\" in {root}.";
            return found;
        }

        if (Chosen(root) is { } chosen)
        {
            var found = CommandWords.IsShellFolder(chosen) ? In(Path.Combine(root, chosen), out error) : null;
            if (found is null && error is null)
                error = $"The chosen shell \"{chosen}\" is not in {root}. Choose one with winshell use <name>.";
            return found;
        }

        if (In(root, out error) is { } atRoot)
            return atRoot;
        if (error is not null)
            return null;
        if (In(Path.Combine(root, "default"), out error) is { } usual)
            return usual;
        if (error is not null)
            return null;
        var names = Names(root);
        if (names.Count > 1)
        {
            error =
                $"Several shells in {root}; choose one with winshell use <name>, or pick one with -s: {string.Join(", ", names)}.";
            return null;
        }

        return names.Count == 1 ? In(Path.Combine(root, names[0]), out error) : null;
    }

    public string? Prepare(bool noBuild, RunProcess run)
    {
        try
        {
            if (noBuild)
                return File.Exists(Program) ? null : $"{Name} has not been built. Run winshell run once to build it.";
            return NeedsBuild() ? Build(run) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }

    public bool NeedsBuild()
    {
        var program = Program;
        if (!File.Exists(program))
            return true;
        var built = File.GetLastWriteTimeUtc(program);
        return Sources(Folder).Any(source => File.GetLastWriteTimeUtc(source) > built);
    }

    public static CommandResult Run(string file, IReadOnlyList<string> arguments, string folder)
    {
        var start = new ProcessStartInfo(file)
        {
            WorkingDirectory = folder,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        var lines = new List<string>();
        var gate = new object();

        Thread Read(StreamReader stream)
        {
            var reader = new Thread(() =>
                {
                    while (stream.ReadLine() is { } line)
                        lock (gate)
                            lines.Add(line);
                })
            { IsBackground = true };
            reader.Start();
            return reader;
        }

        try
        {
            var process = Process.Start(start)!;
            Thread[] readers = [Read(process.StandardOutput), Read(process.StandardError)];
            if (!process.WaitForExit(BuildLimit))
            {
                process.Kill(true);
                return new CommandResult(-1,
                    $"{Path.GetFileName(file)} was still running after {BuildLimit.TotalMinutes} minutes.");
            }

            foreach (var reader in readers)
                reader.Join(StreamLimit);
            lock (gate)
                return new CommandResult(process.ExitCode, string.Join('\n', lines));
        }
        catch (Win32Exception)
        {
            return new CommandResult(-1, NeedsSdk);
        }
    }

    private string? Build(RunProcess run)
    {
        var destination = OutputFolder;
        var parked = Park(destination);
        if (parked is null && Directory.Exists(destination))
        {
            destination = BesideFolder;
            parked = Park(destination);
            if (parked is null && Directory.Exists(destination))
                return $"{Name} is running, and the build could not write a new program beside it.";
        }

        var result = run("dotnet", ["build", ProjectFile, "-c", "Release", "-o", destination, "--nologo", "-v", "q"],
            Folder);
        if (result.ExitCode == 0)
        {
            QuietDelete(parked);
            var built = ProgramIn(destination);
            if (File.Exists(built))
                File.SetLastWriteTimeUtc(built, TimeProvider.System.GetUtcNow().UtcDateTime);
            return null;
        }

        if (parked is not null)
            Restore(destination, parked);
        return $"{Name} did not build.\n{Errors(result.Output, Folder)}";
    }

    private static string? Park(string folder)
    {
        var parked = folder + ".previous";
        QuietDelete(parked);
        if (!Directory.Exists(folder))
            return null;
        try
        {
            Directory.Move(folder, parked);
            return parked;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void Restore(string folder, string parked)
    {
        if (!Directory.Exists(parked))
            return;
        try
        {
            QuietDelete(folder);
            if (!Directory.Exists(folder))
                Directory.Move(parked, folder);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void QuietDelete(string? path)
    {
        if (path is null || !Directory.Exists(path))
            return;
        try
        {
            Directory.Delete(path, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    internal static string Errors(string output, string folder)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var errors = lines.Where(IsErrorLine)
            .Select(line => ErrorLine(line, folder)).Distinct().ToList();
        return string.Join('\n', errors.Count > 0 ? errors.Take(MostErrors) : lines.TakeLast(MostErrors));
    }

    internal static bool IsErrorLine(string line)
    {
        return CompilerError().IsMatch(line);
    }

    [GeneratedRegex(@"\berror [A-Za-z]+\d+:|: error :")]
    private static partial Regex CompilerError();

    internal static string ErrorLine(string line, string folder)
    {
        var error = ProjectSuffix().Replace(line, "");
        var inFolder = folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return error.StartsWith(inFolder, StringComparison.OrdinalIgnoreCase) ? error[inFolder.Length..] : error;
    }

    [GeneratedRegex(@"\s+\[[^\]]*\.csproj(::[^\]]*)?\]$", RegexOptions.IgnoreCase)]
    private static partial Regex ProjectSuffix();

    private string AssemblyName()
    {
        try
        {
            using var reader = XmlReader.Create(ProjectFile,
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var written = XDocument.Load(reader).Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "AssemblyName")?.Value.Trim();
            return string.IsNullOrEmpty(written) || written.Contains('$', StringComparison.Ordinal) ? Name : written;
        }
        catch (Exception e) when (e is XmlException or IOException or UnauthorizedAccessException)
        {
            return Name;
        }
    }

    private static ShellProject? In(string folder, out string? error)
    {
        error = null;
        var projects = Projects(folder);
        if (projects.Length > 1)
        {
            error =
                $"Several projects in {folder}; name one with -p: {string.Join(", ", projects.Select(Path.GetFileName))}.";
            return null;
        }

        return projects.Length == 1 ? new ShellProject(projects[0]) : null;
    }

    private static string[] Projects(string folder)
    {
        return Directory.Exists(folder) ? Directory.GetFiles(folder, "*.csproj") : [];
    }

    private static IEnumerable<string> Sources(string folder)
    {
        foreach (var file in Directory.EnumerateFiles(folder))
            yield return file;
        foreach (var child in Directory.EnumerateDirectories(folder))
        {
            var name = Path.GetFileName(child);
            if (name is "bin" or "obj" || name.StartsWith('.'))
                continue;
            foreach (var file in Sources(child))
                yield return file;
        }
    }
}
