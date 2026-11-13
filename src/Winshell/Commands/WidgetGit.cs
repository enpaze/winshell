namespace Winshell.Commands;

/// <summary>
/// Fetch, clone and log for one widget source. <c>protocol.file.allow=always</c> stays on this git invocation,
/// because a <c>file://</c> URL is otherwise refused when it comes from a remote.
/// </summary>
internal static class WidgetGit
{
    internal const string NeedsGit = "Installing a widget needs git, from https://git-scm.com/download/win.";

    internal const string NeedsGitToUpdate = "Updating a widget needs git, from https://git-scm.com/download/win.";

    /// <summary>Git, with <c>protocol.file.allow=always</c>, using the command's quiet tool variables.</summary>
    internal static CommandResult Run(string git, string[] arguments, string folder)
    {
        return WidgetCommand.Invoke(git, ["-c", "protocol.file.allow=always", .. arguments], folder);
    }

    internal static bool TryClone(string git, WidgetSource source, string commit, string cache, string localRoot,
        out string? error)
    {
        error = null;
        if (Directory.Exists(cache))
        {
            var head = Run(git, ["-C", cache, "rev-parse", "HEAD"], localRoot);
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
            var result = Run(git, step, localRoot);
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

        var checkedOut = Run(git, ["-C", cache, "rev-parse", "HEAD"], localRoot);
        if (checkedOut.ExitCode != 0 || !checkedOut.StandardOutput.Trim().Equals(commit, StringComparison.OrdinalIgnoreCase))
        {
            DeleteTree(cache);
            error = $"The clone of {source.Canonical} is not {commit}.";
            return false;
        }

        MarkReadOnly(cache);
        return true;
    }

    /// <summary><c>git log --oneline old..new</c> in the source repository. A remote ref is fetched into a temporary clone first.</summary>
    internal static bool TryLog(string git, WidgetSource source, string oldCommit, string newCommit, ShellLocations locations,
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
                var result = Run(git, step, locations.LocalRoot);
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

    internal static void MarkReadOnly(string cache)
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

    internal static void DeleteTree(string path)
    {
        if (!Directory.Exists(path))
            return;
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(path, true);
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
        var result = Run(git,
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

    private static bool InDirectory(string file, string name)
    {
        var needle = Path.DirectorySeparatorChar + name + Path.DirectorySeparatorChar;
        return file.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstLine(string error, string output)
    {
        var text = error.Length > 0 ? error : output;
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return line ?? "";
    }
}
