using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Winshell.Commands;

/// <summary>What <c>winshell widget</c> was asked to do. The words are the subcommand and what follows it.</summary>
internal readonly record struct WidgetRequest(
    string? Path,
    string? Config,
    ImmutableArray<string> Words,
    bool Yes,
    string? Project)
{
    public static WidgetRequest From(Invocation parsed)
    {
        return new(parsed.Text(CliFlags.Path), parsed.Text(CliFlags.Config), parsed.Positional,
            parsed.IsSet(CliFlags.Yes), parsed.Text(CliFlags.Project));
    }
}

/// <summary>
/// <c>winshell widget install</c>, <c>list</c>, <c>remove</c> and <c>update</c>. <c>widgets.props</c> beside the
/// configuration is the only record: one <c>ProjectReference</c> per repository, with the source, ref and commit,
/// then that repository's <c>WinshellWidget</c> items. The clone of a commit lives under the local root and is not a
/// second copy of that record. <c>update</c> resolves each recorded ref again and, when the commit moved, builds that
/// commit the same way <c>install</c> builds a new one.
/// </summary>
internal static partial class WidgetCommand
{
    internal static readonly CommandEntry Entry = new()
    {
        Name = "widget",
        Overview =
            "       winshell widget install <source>[@ref] [--yes]\n" +
            "                                           install widgets from a GitHub repository\n" +
            "       winshell widget list                list the widgets a configuration has\n" +
            "       winshell widget remove <name>       remove an installed widget\n" +
            "       winshell widget update [<name>] [--yes]\n" +
            "                                           move installed widgets to the ref's new commit",
        Usage = """
                winshell widget [-c <name> | -p <path>] <command>
                  install <source>[@ref] [--project <path>] [--yes]
                    Installs widgets from github.com/owner/repo, an https://github.com URL, a file:// URL or a folder.
                    @ref is a branch, a tag or a commit. With none, the default branch is used.
                    The command resolves it to a commit, shows the repository, the commit and the
                    widget names, and asks. --yes installs without asking.
                    Building runs this repository's code and MSBuild targets in a program that starts at sign-in.
                    Declining the question changes nothing. Needs git and the .NET SDK.
                  list
                    Lists each installed widget's name, source, ref and commit.
                  remove <name>
                    Removes a widget, or a source, and every widget from that repository.
                  update [<name>] [--yes]
                    Fetches the recorded ref of every installed source, or of the one <name> selects.
                    <name> is a widget name or the source. A ref that still names the recorded
                    commit is up to date. Otherwise the command shows the old and new commit,
                    the commits between them, and the widget names that would be added or dropped,
                    and asks. --yes updates without asking.
                    Building runs this repository's code and MSBuild targets in a program that starts at sign-in.
                    Declining leaves that source as it is. A failed build keeps the previous commit,
                    and older commits stay in the cache. Needs git and the .NET SDK.
                  --project <path>    the project in the repository that lists the widgets
                  -c and -p choose the configuration, as they do for winshell run.
                """,
        Options = [CliFlags.Path, CliFlags.Config],
        Words = WordRule.Subcommand,
        Subcommands =
        [
            new CommandEntry
            {
                Name = "install",
                Options = [CliFlags.Project, CliFlags.Yes],
                MinWords = 1,
                MaxWords = 1,
                Missing = "a source"
            },
            new CommandEntry { Name = "list", MaxWords = 0 },
            new CommandEntry { Name = "remove", MinWords = 1, MaxWords = 1, Missing = "a name" },
            new CommandEntry { Name = "update", Options = [CliFlags.Yes], MaxWords = 1 }
        ],
        Run = static parsed => Task.FromResult(Execute(parsed))
    };

    internal const string NeedsGit = "Installing a widget needs git, from https://git-scm.com/download/win.";

    private const string NeedsGitToUpdate = "Updating a widget needs git, from https://git-scm.com/download/win.";

    private const string Warning =
        "Building runs this repository's code and MSBuild targets in a program that starts at sign-in.";

    private static readonly UTF8Encoding Utf8 = new(false);

    private const string WidgetsFile = "WinshellWidgetsFile";

    /// <summary>What <c>remove</c> leaves when the last source goes. The file stays, so the next build sees it.</summary>
    private const string EmptyProject = "<Project />\n";

    /// <summary>Keeps git and dotnet from prompting, and keeps a build from leaving a server for the next command.</summary>
    private static readonly Dictionary<string, string> QuietTools = new()
    {
        ["GIT_TERMINAL_PROMPT"] = "0",
        ["GCM_INTERACTIVE"] = "never",
        ["DOTNET_NOLOGO"] = "1",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
        ["MSBUILDDISABLENODEREUSE"] = "1"
    };

    /// <summary>Runs <c>widget</c> and prints its result. <c>--yes</c> skips the question; anything else is read from the console.</summary>
    internal static int Execute(Invocation parsed)
    {
        var result = Apply(WidgetRequest.From(parsed), ShellLocations.Current, Console.In, Console.Out, "git", "dotnet");
        if (result.Error is not null)
            Console.Error.WriteLine(result.Error);
        return result.Code;
    }

    /// <summary>
    /// What <see cref="Execute"/> does, against <paramref name="locations"/>, reading a yes or no from <paramref name="input"/>.
    /// <paramref name="git"/> and <paramref name="dotnet"/> are the tools it runs. A missing one is reported and nothing
    /// is written.
    /// </summary>
    internal static Result Apply(WidgetRequest request, ShellLocations locations, TextReader input, TextWriter output,
        string git, string dotnet)
    {
        var project = ConfigurationProject.Find(request.Path, request.Config, locations.ConfigRoot, out var error);
        if (error is not null)
            return Fail(1, error);
        if (project is null)
            return Fail(1, "winshell widget needs a configuration project. Run winshell new to make one.");

        var command = request.Words[0];
        var props = Path.Combine(project.Folder, "widgets.props");
        if (!TryRead(props, out var installed, out error))
            return Fail(1, error!);
        if (command == "list")
            return List(installed, output);
        if (command == "remove")
            return Remove(request.Words[1], props, installed, output);
        if (command == "update")
            return Update(request, project, props, installed, input, output, git, dotnet, locations);

        if (EnsureLocal(locations.LocalRoot) is { } local)
            return local;
        if (RequireTools(git, dotnet, locations.LocalRoot, NeedsGit) is { } tools)
            return tools;
        if (!TryParse(request.Words[1], out var source, out error))
            return Fail(2, error!);
        var prior = installed.FirstOrDefault(item => SameSource(item.Source, source.Canonical));
        if (prior is not null)
        {
            var name = prior.Widgets.FirstOrDefault().Name ?? source.Name;
            return Fail(1, $"{source.Canonical} is already installed at {prior.Commit}. winshell widget remove {name} removes it.");
        }

        if (!TryPin(git, source, locations.LocalRoot, out var pin, out error))
            return Fail(1, error!);
        var cache = Cache(locations.LocalRoot, source, pin.Commit);
        if (!TryClone(git, source, pin.Commit, cache, locations.LocalRoot, out error))
            return Fail(1, error!);
        if (!TrySelectProject(dotnet, cache, request.Project, out var widgetProject, out error))
            return Fail(1, error!);
        if (!TryReadWidgets(dotnet, widgetProject, locations.LocalRoot, out var widgets, out error))
            return Fail(1, error!);
        if (widgets.Count == 0)
            return Fail(1, $"{Path.GetFileName(widgetProject)} lists no widgets.");
        foreach (var widget in widgets)
        {
            var other = installed.FirstOrDefault(item => item.Widgets.Any(have => have.Name == widget.Name));
            if (other is not null)
                return Fail(1, $"{widget.Name} is already installed from {other.Source}. {source.Canonical} also provides it.");
        }

        if (!VersionsAllow(project.ProjectFile, widgetProject, out error))
            return Fail(1, error!);

        output.WriteLine(source.Canonical);
        output.WriteLine($"Commit {pin.Commit}");
        output.WriteLine($"Widgets: {string.Join(", ", widgets.Select(widget => $"{widget.Name} ({widget.Type})"))}");
        output.WriteLine(Warning);
        if (!Agreed(request.Yes, input, output, "Install? [y/N] ", "Nothing was installed."))
            return Ok();

        var next = installed.Append(new Installed(widgetProject, source.Canonical, pin.Ref, pin.Commit, widgets)).ToList();
        if (Stage(dotnet, project, locations, props, Write(next), cache, widgetProject) is { } failed)
            return failed;

        var names = string.Join(", ", widgets.Select(widget => widget.Name));
        output.WriteLine($"Installed {names} from {source.Canonical} at {pin.Commit}.");
        return Ok();
    }

    /// <summary>
    /// Resolves each installed source's recorded ref, or the one <paramref name="request"/> names. An unchanged commit
    /// is reported. A moved one is shown, then built with <see cref="Stage"/> and recorded only when that build succeeds.
    /// </summary>
    private static Result Update(WidgetRequest request, ConfigurationProject project, string props, List<Installed> installed,
        TextReader input, TextWriter output, string git, string dotnet, ShellLocations locations)
    {
        var name = request.Words.Length > 1 ? request.Words[1] : null;
        if (Choose(installed, name, out var chosen) is { } choice)
            return choice;
        if (chosen.Count == 0)
        {
            output.WriteLine("No widgets are installed.");
            return Ok();
        }

        if (EnsureLocal(locations.LocalRoot) is { } local)
            return local;
        if (RequireTools(git, dotnet, locations.LocalRoot, NeedsGitToUpdate) is { } tools)
            return tools;

        var next = installed.ToList();
        foreach (var item in chosen)
        {
            if (!TryParse(item.Source, out var source, out var error))
                return Fail(1, error!);
            source = source with { Ref = item.Ref.Length == 0 ? null : item.Ref };
            if (!TryPin(git, source, locations.LocalRoot, out var pin, out error))
                return Fail(1, error!);
            if (pin.Commit.Equals(item.Commit, StringComparison.OrdinalIgnoreCase))
            {
                output.WriteLine($"{source.Canonical} is up to date at {item.Commit}.");
                continue;
            }

            var cache = Cache(locations.LocalRoot, source, pin.Commit);
            if (!TryClone(git, source, pin.Commit, cache, locations.LocalRoot, out error))
                return Fail(1, error!);
            if (!TryUpdatedProject(locations.LocalRoot, source, item, pin.Commit, out var widgetProject, out error))
                return Fail(1, error!);
            if (!TryReadWidgets(dotnet, widgetProject, locations.LocalRoot, out var widgets, out error))
                return Fail(1, error!);
            if (widgets.Count == 0)
                return Fail(1, $"{Path.GetFileName(widgetProject)} lists no widgets.");
            foreach (var widget in widgets)
            {
                var other = next.FirstOrDefault(have => !SameSource(have.Source, item.Source) &&
                                                         have.Widgets.Any(existing => existing.Name == widget.Name));
                if (other is not null)
                    return Fail(1,
                        $"{widget.Name} is already installed from {other.Source}. {source.Canonical} also provides it.");
            }

            if (!VersionsAllow(project.ProjectFile, widgetProject, out error))
                return Fail(1, error!);
            if (!TryLog(git, source, item.Commit, pin.Commit, locations, out var log, out error))
                return Fail(1, error!);

            var added = widgets.Where(widget => item.Widgets.All(existing => existing.Name != widget.Name))
                .Select(widget => widget.Name).ToList();
            var dropped = item.Widgets.Where(existing => widgets.All(widget => widget.Name != existing.Name))
                .Select(existing => existing.Name).ToList();
            output.WriteLine(source.Canonical);
            output.WriteLine($"{item.Commit}..{pin.Commit}");
            foreach (var line in log.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                output.WriteLine(line);
            output.WriteLine("Added: " + Names(added));
            output.WriteLine("Dropped: " + Names(dropped));
            output.WriteLine(Warning);
            if (!Agreed(request.Yes, input, output, "Update? [y/N] ", "Nothing was updated."))
                continue;

            var index = next.FindIndex(have => ReferenceEquals(have, item));
            next[index] = new Installed(widgetProject, source.Canonical, pin.Ref, pin.Commit, widgets);
            if (Stage(dotnet, project, locations, props, Write(next), cache, widgetProject) is { } failed)
                return failed;
            output.WriteLine($"Updated {Names(widgets.Select(widget => widget.Name))} from {source.Canonical} at {pin.Commit}.");
        }

        return Ok();
    }

    /// <summary>
    /// Restores <paramref name="widgetProject"/> on its own, restores the configuration against an empty props file,
    /// and builds it against a staged <c>widgets.props</c>. The file beside the project is replaced only when that
    /// build succeeds. The clone at <paramref name="cache"/> is left in place either way.
    /// </summary>
    private static Result? Stage(string dotnet, ConfigurationProject project, ShellLocations locations, string props,
        string content, string cache, string widgetProject)
    {
        if (project.Blocked() is { } blocked)
            return Fail(1, blocked);
        var scratch = Path.Combine(locations.CacheRoot, "widget-build", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(scratch);
            var staged = Path.Combine(scratch, "widgets.props");
            var empty = Path.Combine(scratch, "empty.props");
            File.WriteAllText(staged, content, Utf8);
            File.WriteAllText(empty, EmptyProject, Utf8);
            // Package props are imported only after a project has an assets file, so the widget is restored on its own
            // before the configuration can see it. The clone is not under the configuration, so that restore would miss
            // the nuget.config winshell new wrote beside the project. RestoreRootConfigDirectory is the folder NuGet
            // walks up from, and the SDK forwards it to GetRestoreSettingsTask. The Winshell package is not on
            // nuget.org. NuGet's restore then walks project-reference paths and drops their metadata, which would hand
            // the staged file to the library and make the library reference itself. The reference clears that property
            // for the build. Restore the configuration against an empty file, then build without restoring.
            var restored = Invoke(dotnet,
            [
                "restore", widgetProject, "--nologo", "-nodeReuse:false",
                "-p:RestoreRootConfigDirectory=" + project.Folder
            ], locations.LocalRoot);
            if (Failed(restored, Path.GetFileName(widgetProject), "restore", Path.GetDirectoryName(widgetProject)!) is { } widgetRestore)
                return widgetRestore;
            var prepared = Invoke(dotnet,
            [
                "restore", project.ProjectFile, "--nologo", "-nodeReuse:false", "-p:" + WidgetsFile + "=" + empty
            ], project.Folder);
            if (Failed(prepared, project.Name, "restore", project.Folder) is { } configurationRestore)
                return configurationRestore;
            var built = Invoke(dotnet,
            [
                "build", project.ProjectFile, "-c", "Release", "-o", Path.Combine(scratch, "out"),
                "-p:" + WidgetsFile + "=" + staged, "--no-restore", "--nologo", "-v", "q", "-nodeReuse:false"
            ], project.Folder);
            if (Failed(built, project.Name, "build", project.Folder) is { } configurationBuild)
                return configurationBuild;
            MarkReadOnly(cache);
            Replace(props, content);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Fail(1, e.Message);
        }
        finally
        {
            DeleteTree(scratch);
        }
    }

    internal readonly record struct Result(int Code, string? Error);

    /// <summary>A repository argument: a GitHub repository, a <c>file://</c> URL, or a folder, and the ref after <c>@</c>.</summary>
    internal readonly record struct WidgetSource(
        string Canonical, string GitUrl, string? Ref, string CacheOwner, string CacheRepo, string Name);

    /// <summary>
    /// Reads <paramref name="text"/> as <c>github.com/owner/repo</c>, an <c>https://github.com</c> URL, a <c>file://</c>
    /// URL or a folder. <c>@ref</c> is the branch, tag or commit. Nothing is contacted.
    /// </summary>
    internal static bool TryParse(string text, out WidgetSource source, out string? error)
    {
        source = default;
        error = null;
        text = text.Trim();
        if (GitHubSource().Match(text) is { Success: true } match)
        {
            var owner = match.Groups["owner"].Value;
            var repo = match.Groups["repo"].Value;
            if (owner is "." or ".." || repo is "." or "..")
            {
                error = $"\"{text}\" is not a GitHub repository. Use github.com/owner/repo, or a local path.";
                return false;
            }

            var reference = match.Groups["ref"].Success ? match.Groups["ref"].Value : null;
            if (!RefOk(reference, text, out error))
                return false;
            source = new WidgetSource($"github.com/{owner}/{repo}", $"https://github.com/{owner}/{repo}.git", reference,
                Sanitize(owner), Sanitize(repo), repo);
            return true;
        }

        if (text.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return TryFile(text, out source, out error);
        if (TryFolder(text, out source, out error))
            return error is null;
        error ??= $"\"{text}\" is not a GitHub repository. Use github.com/owner/repo, a file:// URL, or a local path.";
        return false;
    }

    private static Result List(List<Installed> installed, TextWriter output)
    {
        if (installed.Count == 0 || installed.All(item => item.Widgets.Count == 0))
        {
            output.WriteLine("No widgets are installed.");
            return Ok();
        }

        foreach (var item in installed)
        {
            foreach (var widget in item.Widgets)
            {
                output.WriteLine(widget.Name);
                output.WriteLine($"  source: {item.Source}");
                output.WriteLine($"  ref: {item.Ref}");
                output.WriteLine($"  commit: {item.Commit}");
            }
        }

        return Ok();
    }

    private static Result Remove(string name, string props, List<Installed> installed, TextWriter output)
    {
        var matches = Matches(installed, name);
        if (matches.Count == 0)
            return Fail(1, $"No installed widget or source is named \"{name}\".");
        if (matches.Count > 1)
            return Fail(1, $"\"{name}\" matches more than one installed source: {string.Join(", ", matches.Select(item => item.Source))}.");

        var kept = installed.Where(item => !ReferenceEquals(item, matches[0])).ToList();
        try
        {
            // Deleting the last file leaves nothing newer than the program, so the next run would keep the old one.
            // An empty project is still a source, and writing it makes that check rebuild.
            Replace(props, kept.Count == 0 ? EmptyProject : Write(kept));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Fail(1, e.Message);
        }

        output.WriteLine($"Removed {matches[0].Source}.");
        return Ok();
    }

    private static Result? EnsureLocal(string localRoot)
    {
        try
        {
            Directory.CreateDirectory(localRoot);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Fail(1, e.Message);
        }
    }

    private static Result? RequireTools(string git, string dotnet, string localRoot, string gitMissing)
    {
        if (!Tool(git, localRoot, out var missing) && missing)
            return Fail(1, gitMissing);
        if (!Tool(dotnet, localRoot, out missing) && missing)
            return Fail(1, ConfigurationProject.NeedsSdk);
        return null;
    }

    /// <summary><c>--yes</c> agrees. Otherwise the answer is read from <paramref name="input"/>, and a no prints <paramref name="declined"/>.</summary>
    private static bool Agreed(bool yes, TextReader input, TextWriter output, string question, string declined)
    {
        if (yes)
            return true;
        output.Write(question);
        output.Flush();
        var answer = input.ReadLine()?.Trim() ?? "";
        if (answer.Equals("y", StringComparison.OrdinalIgnoreCase) ||
            answer.Equals("yes", StringComparison.OrdinalIgnoreCase))
            return true;
        output.WriteLine(declined);
        return false;
    }

    /// <summary>No name selects every installed source. A name selects one widget or source, with <c>remove</c>'s errors.</summary>
    private static Result? Choose(List<Installed> installed, string? name, out List<Installed> chosen)
    {
        if (name is null)
        {
            chosen = installed.Count == 0 || installed.All(item => item.Widgets.Count == 0) ? [] : installed.ToList();
            return null;
        }

        chosen = Matches(installed, name);
        if (chosen.Count == 0)
            return Fail(1, $"No installed widget or source is named \"{name}\".");
        if (chosen.Count > 1)
            return Fail(1,
                $"\"{name}\" matches more than one installed source: {string.Join(", ", chosen.Select(item => item.Source))}.");
        return null;
    }

    private static List<Installed> Matches(List<Installed> installed, string name)
    {
        return installed.Where(item => item.Widgets.Any(widget => widget.Name == name) || SourceNamed(item, name))
            .Distinct()
            .ToList();
    }

    private static bool SourceNamed(Installed item, string name)
    {
        if (item.Source.Equals(name, StringComparison.OrdinalIgnoreCase))
            return true;
        var repo = item.Source.Split('/', '\\').LastOrDefault(part => part.Length > 0) ?? "";
        if (repo.Equals(name, StringComparison.OrdinalIgnoreCase))
            return true;
        var owner = item.Source.Split('/', '\\').Where(part => part.Length > 0).ToArray();
        return owner.Length >= 2 && $"{owner[^2]}/{owner[^1]}".Equals(name, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameSource(string installed, string canonical)
    {
        return installed.Equals(canonical, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The same project path, inside the clone of <paramref name="commit"/>.</summary>
    private static bool TryUpdatedProject(string localRoot, WidgetSource source, Installed item, string commit,
        out string widgetProject, out string? error)
    {
        widgetProject = "";
        error = null;
        var oldCache = Cache(localRoot, source, item.Commit);
        if (!Inside(oldCache, item.Project))
        {
            error = $"{item.Project} is not a project in {source.Canonical}.";
            return false;
        }

        var relative = Relative(oldCache, item.Project);
        var candidate = Path.GetFullPath(Path.Combine(Cache(localRoot, source, commit), relative));
        if (!File.Exists(candidate))
        {
            error = $"{relative} is not in the new commit of {source.Canonical}.";
            return false;
        }

        widgetProject = candidate;
        return true;
    }

    /// <summary><c>git log --oneline old..new</c> in the source repository. A remote ref is fetched into a temporary clone first.</summary>
    private static bool TryLog(string git, WidgetSource source, string oldCommit, string newCommit, ShellLocations locations,
        out string log, out string? error)
    {
        var repo = LocalRepository(source);
        if (repo is not null)
            return ReadLog(git, repo, oldCommit, newCommit, locations.LocalRoot, out log, out error);

        log = "";
        error = null;
        var scratch = Path.Combine(locations.CacheRoot, "widget-log", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(scratch);
            string[][] steps =
            [
                ["-C", scratch, "init", "--quiet"],
                ["-C", scratch, "remote", "add", "origin", source.GitUrl],
                ["-C", scratch, "fetch", "--quiet", "origin", source.Ref ?? "HEAD"]
            ];
            foreach (var step in steps)
            {
                var result = Git(git, step, locations.LocalRoot);
                if (result.Missing)
                {
                    error = NeedsGitToUpdate;
                    return false;
                }

                if (result.ExitCode != 0)
                {
                    var detail = FirstLine(result.Error, result.StandardOutput);
                    error = detail.Length == 0
                        ? $"Could not read {source.Canonical}."
                        : $"Could not read {source.Canonical}: {detail}";
                    return false;
                }
            }

            return ReadLog(git, scratch, oldCommit, newCommit, locations.LocalRoot, out log, out error);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            error = e.Message;
            return false;
        }
        finally
        {
            DeleteTree(scratch);
        }
    }

    private static string? LocalRepository(WidgetSource source)
    {
        if (Directory.Exists(source.GitUrl))
            return source.GitUrl;
        if (Uri.TryCreate(source.GitUrl, UriKind.Absolute, out var uri) && uri.IsFile && Directory.Exists(uri.LocalPath))
            return uri.LocalPath;
        return null;
    }

    private static bool ReadLog(string git, string repo, string oldCommit, string newCommit, string localRoot,
        out string log, out string? error)
    {
        log = "";
        error = null;
        var result = Git(git,
        [
            "-c", "color.ui=never", "--no-pager", "-C", repo, "log", "--oneline", $"{oldCommit}..{newCommit}"
        ], localRoot);
        if (result.Missing)
        {
            error = NeedsGitToUpdate;
            return false;
        }

        if (result.ExitCode != 0)
        {
            var detail = FirstLine(result.Error, result.StandardOutput);
            error = detail.Length == 0 ? $"Could not read the commits between {oldCommit} and {newCommit}." : detail;
            return false;
        }

        log = result.StandardOutput;
        return true;
    }

    private static string Names(IEnumerable<string> names)
    {
        var text = string.Join(", ", names);
        return text.Length == 0 ? "none" : text;
    }

    private static bool TryFile(string text, out WidgetSource source, out string? error)
    {
        source = default;
        error = null;
        var body = text;
        string? reference = null;
        if (!ExistingFileUrl(text) && text.LastIndexOf('@') is var at and > 5)
        {
            body = text[..at];
            reference = text[(at + 1)..];
        }

        if (!Uri.TryCreate(body, UriKind.Absolute, out var uri) || !uri.IsFile)
        {
            error = $"\"{text}\" is not a file URL.";
            return false;
        }

        if (!RefOk(reference, text, out error))
            return false;
        var path = uri.LocalPath;
        var canonical = uri.AbsoluteUri;
        source = new WidgetSource(canonical, canonical, reference, "local", CacheRepo(path, canonical),
            Path.GetFileName(Path.TrimEndingDirectorySeparator(path)));
        return true;
    }

    private static bool TryFolder(string text, out WidgetSource source, out string? error)
    {
        source = default;
        error = null;
        var body = text;
        string? reference = null;
        if (Directory.Exists(text))
            body = text;
        else if (text.LastIndexOf('@') is var at and > 1 && Directory.Exists(text[..at]))
        {
            body = text[..at];
            reference = text[(at + 1)..];
        }
        else if (!LooksLikePath(text))
            return false;
        else if (text.LastIndexOf('@') is var cut and > 1 && LooksLikePath(text[..cut]))
        {
            body = text[..cut];
            reference = text[(cut + 1)..];
        }

        string full;
        try
        {
            full = Path.GetFullPath(body);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"\"{text}\" is not a folder.";
            return false;
        }

        if (!RefOk(reference, text, out error))
            return false;
        if (!Directory.Exists(full))
        {
            error = $"{full} is not a folder.";
            return false;
        }

        source = new WidgetSource(full, full, reference, "local", CacheRepo(full, full),
            Path.GetFileName(Path.TrimEndingDirectorySeparator(full)));
        return true;
    }

    private static bool LooksLikePath(string text)
    {
        return text.Contains('\\', StringComparison.Ordinal) || text.Contains(':', StringComparison.Ordinal) ||
               text.StartsWith('/') || text.StartsWith('.');
    }

    private static bool ExistingFileUrl(string text)
    {
        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.IsFile && Directory.Exists(uri.LocalPath);
    }

    private static bool RefOk(string? reference, string text, out string? error)
    {
        error = null;
        if (reference is null)
            return true;
        if (reference.Length == 0 || reference.StartsWith('-') || reference.Any(char.IsWhiteSpace))
        {
            error = $"\"{text}\" has no usable ref. A ref is a branch, a tag or a commit.";
            return false;
        }

        return true;
    }

    private readonly record struct Pin(string Commit, string Ref);

    private static bool TryPin(string git, WidgetSource source, string localRoot, out Pin pin, out string? error)
    {
        pin = default;
        error = null;
        var reference = source.Ref;
        if (reference is not null && IsCommit(reference))
        {
            pin = new Pin(reference.ToLowerInvariant(), reference);
            return true;
        }

        if (reference is null || reference.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
        {
            var head = Git(git, ["ls-remote", "--symref", source.GitUrl, "HEAD"], localRoot);
            if (!GitOk(head, source, out error))
                return false;
            if (!TryHead(head.StandardOutput, out var commit, out var branch))
            {
                error = $"{source.Canonical} has no commits.";
                return false;
            }

            pin = new Pin(commit, branch.Length > 0 ? branch : "HEAD");
            return true;
        }

        var listed = Git(git,
        [
            "ls-remote", source.GitUrl, "refs/heads/" + reference, "refs/tags/" + reference,
            "refs/tags/" + reference + "^{}"
        ], localRoot);
        if (!GitOk(listed, source, out error))
            return false;
        var commitOf = CommitOf(listed.StandardOutput);
        if (commitOf is null)
        {
            error = $"No ref \"{reference}\" in {source.Canonical}.";
            return false;
        }

        pin = new Pin(commitOf, reference);
        return true;
    }

    private static bool GitOk(CommandResult result, WidgetSource source, out string? error)
    {
        error = null;
        if (result.Missing)
        {
            error = NeedsGit;
            return false;
        }

        if (result.ExitCode == 0)
            return true;
        var detail = FirstLine(result.Error, result.StandardOutput);
        error = detail.Length == 0
            ? $"Could not read {source.Canonical}."
            : $"Could not read {source.Canonical}: {detail}";
        return false;
    }

    private static bool TryHead(string output, out string commit, out string branch)
    {
        commit = "";
        branch = "";
        foreach (var (sha, name) in Refs(output))
        {
            if (sha.StartsWith("ref:", StringComparison.Ordinal) && name == "HEAD")
            {
                var named = sha["ref:".Length..].Trim();
                const string heads = "refs/heads/";
                branch = named.StartsWith(heads, StringComparison.Ordinal) ? named[heads.Length..] : named;
            }
            else if (name == "HEAD" && IsCommit(sha))
            {
                commit = sha.ToLowerInvariant();
            }
        }

        return commit.Length > 0;
    }

    private static string? CommitOf(string output)
    {
        string? found = null;
        foreach (var (sha, name) in Refs(output))
        {
            if (!IsCommit(sha))
                continue;
            if (name.EndsWith("^{}", StringComparison.Ordinal))
                return sha.ToLowerInvariant();
            found ??= sha.ToLowerInvariant();
        }

        return found;
    }

    private static IEnumerable<(string Sha, string Name)> Refs(string output)
    {
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            var tab = line.IndexOf('\t');
            if (tab > 0)
                yield return (line[..tab], line[(tab + 1)..]);
        }
    }

    private static bool IsCommit(string text)
    {
        return text.Length is 40 or 64 && text.All(char.IsAsciiHexDigit);
    }

    private static string Cache(string localRoot, WidgetSource source, string commit)
    {
        return Path.Combine(localRoot, "widgets", source.CacheOwner, source.CacheRepo, commit);
    }

    private static string CacheRepo(string path, string canonical)
    {
        var name = Sanitize(Path.GetFileName(Path.TrimEndingDirectorySeparator(path)));
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..8];
        return name + "-" + hash;
    }

    private static string Sanitize(string name)
    {
        var chars = name.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '_').ToArray();
        var text = new string(chars).Trim('_');
        if (text.Length == 0 || text is "." or "..")
            text = "widget";
        return text.Length > 40 ? text[..40] : text;
    }

    private static bool TryClone(string git, WidgetSource source, string commit, string cache, string localRoot,
        out string? error)
    {
        error = null;
        if (Directory.Exists(cache))
        {
            var head = Git(git, ["-C", cache, "rev-parse", "HEAD"], localRoot);
            if (head.ExitCode == 0 && head.StandardOutput.Trim().Equals(commit, StringComparison.OrdinalIgnoreCase))
                return true;
            DeleteTree(cache);
        }

        Directory.CreateDirectory(cache);
        string[][] steps =
        [
            ["-C", cache, "init", "--quiet"],
            ["-C", cache, "remote", "add", "origin", source.GitUrl],
            ["-C", cache, "fetch", "--depth", "1", "origin", commit],
            ["-C", cache, "checkout", "--detach", "--quiet", "FETCH_HEAD"],
            ["-C", cache, "remote", "remove", "origin"]
        ];
        foreach (var step in steps)
        {
            var result = Git(git, step, localRoot);
            if (result.Missing || result.ExitCode != 0)
            {
                DeleteTree(cache);
                if (result.Missing)
                {
                    error = NeedsGit;
                    return false;
                }

                var detail = FirstLine(result.Error, result.StandardOutput);
                error = detail.Length == 0
                    ? $"Could not clone {source.Canonical}."
                    : $"Could not clone {source.Canonical}: {detail}";
                return false;
            }
        }

        var checkedOut = Git(git, ["-C", cache, "rev-parse", "HEAD"], localRoot);
        if (checkedOut.ExitCode != 0 || !checkedOut.StandardOutput.Trim().Equals(commit, StringComparison.OrdinalIgnoreCase))
        {
            DeleteTree(cache);
            error = $"The clone of {source.Canonical} is not {commit}.";
            return false;
        }

        MarkReadOnly(cache);
        return true;
    }

    private static void MarkReadOnly(string cache)
    {
        foreach (var file in Directory.EnumerateFiles(cache, "*", SearchOption.AllDirectories))
        {
            if (InDirectory(file, ".git") || InDirectory(file, "bin") || InDirectory(file, "obj"))
                continue;
            var attributes = File.GetAttributes(file);
            if ((attributes & FileAttributes.ReadOnly) == 0)
                File.SetAttributes(file, attributes | FileAttributes.ReadOnly);
        }
    }

    private static bool InDirectory(string file, string name)
    {
        var needle = Path.DirectorySeparatorChar + name + Path.DirectorySeparatorChar;
        return file.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TrySelectProject(string dotnet, string cache, string? project, out string widgetProject,
        out string? error)
    {
        widgetProject = "";
        error = null;
        if (project is not null)
        {
            string full;
            try
            {
                full = Path.GetFullPath(Path.IsPathRooted(project) ? project : Path.Combine(cache, project));
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
            {
                error = $"\"{project}\" is not a project in the repository.";
                return false;
            }

            if (!Inside(cache, full) || !File.Exists(full) ||
                !full.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                error = $"\"{project}\" is not a project in the repository.";
                return false;
            }

            widgetProject = full;
            return true;
        }

        var found = new List<string>();
        foreach (var file in Projects(cache))
        {
            if (!HasWidgets(dotnet, file, cache, out var any, out error))
                return false;
            if (any)
                found.Add(file);
        }

        if (found.Count == 1)
        {
            widgetProject = found[0];
            return true;
        }

        if (found.Count == 0)
        {
            error = "No project in the repository lists a WinshellWidget item. Name one with --project.";
            return false;
        }

        var names = string.Join("\n", found.Select(file => Relative(cache, file)).Order(StringComparer.Ordinal));
        error = "Several projects in the repository list widgets. Name one with --project:\n" + names;
        return false;
    }

    private static bool HasWidgets(string dotnet, string project, string localRoot, out bool any, out string? error)
    {
        any = false;
        if (!TryReadWidgets(dotnet, project, localRoot, out var widgets, out error))
            return false;
        any = widgets.Count > 0;
        error = null;
        return true;
    }

    private static bool TryReadWidgets(string dotnet, string project, string localRoot, out List<WidgetItem> widgets,
        out string? error)
    {
        widgets = [];
        error = null;
        var result = Invoke(dotnet,
        [
            "msbuild", project, "-getItem:WinshellWidget", "-nologo", "-restore:false", "-nodeReuse:false"
        ], localRoot);
        if (result.Missing)
        {
            error = ConfigurationProject.NeedsSdk;
            return false;
        }

        if (result.ExitCode != 0)
        {
            error = $"{Path.GetFileName(project)} could not be read.\n{FirstLine(result.Error, result.StandardOutput)}";
            return false;
        }

        var start = result.StandardOutput.IndexOf('{');
        var end = result.StandardOutput.LastIndexOf('}');
        if (start < 0 || end < start)
        {
            error = $"{Path.GetFileName(project)} listed no widget items.";
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(result.StandardOutput[start..(end + 1)]);
            if (!json.RootElement.TryGetProperty("Items", out var items) ||
                !items.TryGetProperty("WinshellWidget", out var listed))
                return true;
            foreach (var item in listed.EnumerateArray())
            {
                var widgetName = item.TryGetProperty("Identity", out var identity) ? identity.GetString() ?? "" : "";
                var type = item.TryGetProperty("Type", out var typeValue) ? typeValue.GetString() ?? "" : "";
                if (widgetName.Length == 0 || type.Length == 0)
                {
                    error = $"A WinshellWidget item in {Path.GetFileName(project)} needs a name and a Type.";
                    return false;
                }

                if (widgets.Any(widget => widget.Name == widgetName))
                {
                    error = $"Widget \"{widgetName}\" is listed twice in {Path.GetFileName(project)}.";
                    return false;
                }

                widgets.Add(new WidgetItem(widgetName, type));
            }
        }
        catch (JsonException)
        {
            error = $"{Path.GetFileName(project)} did not list its widgets as JSON.";
            return false;
        }

        return true;
    }

    private static IEnumerable<string> Projects(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            IEnumerable<string> files;
            IEnumerable<string> children;
            try
            {
                files = Directory.EnumerateFiles(dir, "*.csproj").Order(StringComparer.Ordinal).ToList();
                children = Directory.EnumerateDirectories(dir).Order(StringComparer.Ordinal).ToList();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
                yield return file;
            foreach (var child in children)
            {
                var name = Path.GetFileName(child);
                if (name is "bin" or "obj" or ".git")
                    continue;
                pending.Push(child);
            }
        }
    }

    private static bool Inside(string root, string full)
    {
        var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return full.Equals(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) ||
               full.StartsWith(directory, StringComparison.OrdinalIgnoreCase);
    }

    private static string Relative(string root, string full)
    {
        var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return full.StartsWith(directory, StringComparison.OrdinalIgnoreCase) ? full[directory.Length..] : full;
    }

    private static bool VersionsAllow(string configuration, string widget, out string? error)
    {
        error = null;
        var required = PackageVersion(widget);
        var used = PackageVersion(configuration);
        if (required.Text is null || used.Text is null)
            return true;
        if (!TryVersion(required.Text, out var need) || !TryVersion(used.Text, out var have))
        {
            error = required.Text is null || !TryVersion(required.Text, out _)
                ? $"The widget's Winshell version \"{required.Text}\" is not a version number, so it was not installed."
                : $"This configuration's Winshell version \"{used.Text}\" is not a version number, so the widget was not installed.";
            return false;
        }

        if (Compare(need, have) > 0)
        {
            error = $"This widget needs Winshell {required.Text} and this configuration uses {used.Text}.";
            return false;
        }

        return true;
    }

    private readonly record struct VersionText(string? Text);

    private static VersionText PackageVersion(string project)
    {
        try
        {
            using var reader = XmlReader.Create(project,
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var reference = XDocument.Load(reader).Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "PackageReference" &&
                                            WinshellPackage(element));
            if (reference is null)
                return new VersionText(null);
            var version = (string?)reference.Attribute("Version") ?? reference.Elements()
                .FirstOrDefault(element => element.Name.LocalName == "Version")?.Value;
            return new VersionText(string.IsNullOrWhiteSpace(version) ? "" : version.Trim());
        }
        catch (Exception e) when (e is XmlException or IOException or UnauthorizedAccessException)
        {
            return new VersionText("");
        }
    }

    private static bool WinshellPackage(XElement element)
    {
        var name = (string?)element.Attribute("Include") ?? (string?)element.Attribute("Update");
        return name is not null && name.Equals("Winshell", StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct Version(int Major, int Minor, int Patch, string? Pre);

    private static bool TryVersion(string text, out Version version)
    {
        version = default;
        var core = text.Trim();
        var plus = core.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
            core = core[..plus];
        string? pre = null;
        var dash = core.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
        {
            pre = core[(dash + 1)..];
            core = core[..dash];
            if (pre.Length == 0)
                return false;
        }

        var parts = core.Split('.');
        if (parts.Length is < 1 or > 3)
            return false;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major) || major < 0)
            return false;
        var minor = parts.Length > 1 ? Part(parts[1]) : 0;
        var patch = parts.Length > 2 ? Part(parts[2]) : 0;
        if (minor < 0 || patch < 0)
            return false;
        version = new Version(major, minor, patch, pre);
        return true;
    }

    private static int Part(string text)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value
            : -1;
    }

    private static int Compare(Version left, Version right)
    {
        var compared = left.Major.CompareTo(right.Major);
        if (compared != 0)
            return compared;
        compared = left.Minor.CompareTo(right.Minor);
        if (compared != 0)
            return compared;
        compared = left.Patch.CompareTo(right.Patch);
        if (compared != 0)
            return compared;
        if (left.Pre is null && right.Pre is null)
            return 0;
        if (left.Pre is null)
            return 1;
        if (right.Pre is null)
            return -1;
        return string.CompareOrdinal(left.Pre, right.Pre);
    }

    private sealed class Installed(string project, string source, string reference, string commit, IReadOnlyList<WidgetItem> widgets)
    {
        public string Project { get; } = project;

        public string Source { get; } = source;

        public string Ref { get; } = reference;

        public string Commit { get; } = commit;

        public List<WidgetItem> Widgets { get; } = [.. widgets];
    }

    private readonly record struct WidgetItem(string Name, string Type);

    private static bool TryRead(string path, out List<Installed> installed, out string? error)
    {
        installed = [];
        error = null;
        if (!File.Exists(path))
            return true;
        try
        {
            using var reader = XmlReader.Create(path,
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var document = XDocument.Load(reader);
            Installed? current = null;
            foreach (var element in document.Descendants())
            {
                if (element.Name.LocalName == "ProjectReference")
                {
                    current = new Installed((string?)element.Attribute("Include") ?? "", Child(element, "WinshellSource"),
                        Child(element, "WinshellRef"), Child(element, "WinshellCommit"), []);
                    installed.Add(current);
                }
                else if (element.Name.LocalName == "WinshellWidget")
                {
                    var widget = new WidgetItem((string?)element.Attribute("Include") ?? "",
                        (string?)element.Attribute("Type") ?? "");
                    var source = Child(element, "WinshellSource");
                    var home = source.Length > 0
                        ? installed.FirstOrDefault(item => item.Source == source) ?? current
                        : current;
                    if (home is null)
                    {
                        home = new Installed("", source, "", "", []);
                        installed.Add(home);
                    }

                    home.Widgets.Add(widget);
                }
            }
        }
        catch (Exception e) when (e is XmlException or IOException or UnauthorizedAccessException)
        {
            error = $"{path} could not be read: {e.Message}";
            return false;
        }

        return true;
    }

    private static string Child(XElement element, string name)
    {
        return element.Elements().FirstOrDefault(child => child.Name.LocalName == name)?.Value.Trim() ?? "";
    }

    private static string Write(IReadOnlyList<Installed> installed)
    {
        var group = new XElement("ItemGroup");
        foreach (var item in installed)
        {
            // UndefineProperties is project-reference metadata the MSBuild task reads. The library then keeps its own
            // widgets file instead of this one, so it does not reference itself.
            group.Add(new XElement("ProjectReference",
                new XAttribute("Include", item.Project),
                new XElement("WinshellSource", item.Source),
                new XElement("WinshellRef", item.Ref),
                new XElement("WinshellCommit", item.Commit),
                new XElement("UndefineProperties", WidgetsFile)));
            foreach (var widget in item.Widgets)
            {
                group.Add(new XElement("WinshellWidget",
                    new XAttribute("Include", widget.Name),
                    new XAttribute("Type", widget.Type),
                    new XElement("WinshellSource", item.Source)));
            }
        }

        var output = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n"
        };
        using (var writer = XmlWriter.Create(output, settings))
        {
            new XElement("Project", group).Save(writer);
        }

        return output.Append('\n').ToString();
    }

    private static void Replace(string path, string content)
    {
        var temp = path + ".new";
        File.WriteAllText(temp, content, Utf8);
        try
        {
            File.Move(temp, path, true);
        }
        catch
        {
            if (File.Exists(temp))
                File.Delete(temp);
            throw;
        }
    }

    private static bool Tool(string file, string folder, out bool missing)
    {
        var result = Invoke(file, ["--version"], folder);
        missing = result.Missing;
        return result.ExitCode == 0 && !result.Missing;
    }

    /// <summary>Git, allowed to fetch a <c>file://</c> URL. That protocol is otherwise refused when it comes from a remote.</summary>
    private static CommandResult Git(string git, IReadOnlyList<string> arguments, string folder)
    {
        return Invoke(git, ["-c", "protocol.file.allow=always", .. arguments], folder);
    }

    /// <summary><see cref="ConfigurationProject.Run"/>, with the variables git and dotnet need so they do not prompt.</summary>
    private static CommandResult Invoke(string file, IReadOnlyList<string> arguments, string folder)
    {
        return ConfigurationProject.Run(file, arguments, folder, QuietTools);
    }

    /// <summary>A tool that did not start, or that exited with an error. Null when it succeeded.</summary>
    private static Result? Failed(CommandResult result, string name, string action, string folder)
    {
        if (result.Missing)
            return Fail(1, ConfigurationProject.NeedsSdk);
        if (result.ExitCode != 0)
            return Fail(1, $"{name} did not {action}.\n{ConfigurationProject.Errors(result.Output, folder)}");
        return null;
    }

    private static string FirstLine(string error, string output)
    {
        var text = error.Length > 0 ? error : output;
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return line ?? "";
    }

    private static void DeleteTree(string path)
    {
        if (!Directory.Exists(path))
            return;
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(path, true);
    }

    private static Result Fail(int code, string error)
    {
        return new Result(code, error);
    }

    private static Result Ok()
    {
        return new Result(0, null);
    }

    [GeneratedRegex(
        @"^(?:https://)?github\.com/(?<owner>[A-Za-z0-9_.-]+)/(?<repo>[A-Za-z0-9_.-]+?)(?:\.git)?(?:@(?<ref>.+))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GitHubSource();
}
