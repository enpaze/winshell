using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Winshell.Commands;

/// <summary>A repository argument: a GitHub repository, a <c>file://</c> URL, or a folder, and the ref after <c>@</c>.</summary>
internal readonly partial record struct WidgetSource(
    string Canonical, string GitUrl, string? Ref, string CacheOwner, string CacheRepo, string Name)
{
    /// <summary>
    /// Reads <paramref name="text"/> as <c>github.com/owner/repo</c>, an <c>https://github.com</c> URL, a <c>file://</c>
    /// URL or a folder. <c>@ref</c> is the branch, tag or commit. Nothing is contacted.
    /// </summary>
    internal static bool TryParse(string text, out WidgetSource source, out string? error)
    {
        source = default;
        error = null;
        text = text.Trim();
        var parsed = SplitSource(text);
        if (parsed.GitHub is not null)
            return FromGitHub(parsed.GitHub, parsed.Reference, text, out source, out error);
        if (parsed.File)
            return TryFile(text, parsed.Body, parsed.Reference, out source, out error);
        if (parsed.Folder && TryFolder(text, parsed.Body, parsed.Reference, out source, out error))
            return error is null;
        error ??= $"\"{text}\" is not a GitHub repository. Use github.com/owner/repo, a file:// URL, or a local path.";
        return false;
    }

    /// <summary>
    /// The <c>@ref</c> on a GitHub source, a <c>file://</c> URL or a folder. A GitHub ref may itself contain <c>@</c>.
    /// A path that exists is the whole source, so an <c>@</c> in it is not a ref.
    /// </summary>
    private static Parsed SplitSource(string text)
    {
        if (GitHubSource().Match(text) is { Success: true } github)
        {
            var reference = github.Groups["ref"].Success ? github.Groups["ref"].Value : null;
            var body = reference is null ? text : text[..(github.Groups["ref"].Index - 1)];
            return new Parsed(body, reference, github, false, false);
        }

        var at = text.LastIndexOf('@');
        if (text.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            if (at > 5 && !ExistingFileUrl(text))
                return new Parsed(text[..at], text[(at + 1)..], null, true, false);
            return new Parsed(text, null, null, true, false);
        }

        if (Directory.Exists(text))
            return new Parsed(text, null, null, false, true);
        if (at > 1 && Directory.Exists(text[..at]))
            return new Parsed(text[..at], text[(at + 1)..], null, false, true);
        if (!LooksLikePath(text))
            return new Parsed(text, null, null, false, false);
        if (at > 1 && LooksLikePath(text[..at]))
            return new Parsed(text[..at], text[(at + 1)..], null, false, true);
        return new Parsed(text, null, null, false, true);
    }

    private static bool FromGitHub(Match github, string? reference, string text, out WidgetSource source, out string? error)
    {
        source = default;
        error = null;
        var owner = github.Groups["owner"].Value;
        var repo = github.Groups["repo"].Value;
        if (owner is "." or ".." || repo is "." or "..")
        {
            error = $"\"{text}\" is not a GitHub repository. Use github.com/owner/repo, or a local path.";
            return false;
        }

        if (!RefOk(reference, text, out error))
            return false;
        source = new WidgetSource($"github.com/{owner}/{repo}", $"https://github.com/{owner}/{repo}.git", reference,
            Sanitize(owner), Sanitize(repo), repo);
        return true;
    }

    private static bool TryFile(string text, string body, string? reference, out WidgetSource source, out string? error)
    {
        source = default;
        error = null;
        if (!Uri.TryCreate(body, UriKind.Absolute, out var uri) || !uri.IsFile)
        {
            error = $"\"{text}\" is not a file URL.";
            return false;
        }

        if (!RefOk(reference, text, out error))
            return false;
        var path = uri.LocalPath;
        var canonical = uri.AbsoluteUri;
        source = new WidgetSource(canonical, canonical, reference, "local", CacheName(path, canonical),
            Path.GetFileName(Path.TrimEndingDirectorySeparator(path)));
        return true;
    }

    private static bool TryFolder(string text, string body, string? reference, out WidgetSource source, out string? error)
    {
        source = default;
        error = null;
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

        source = new WidgetSource(full, full, reference, "local", CacheName(full, full),
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

    private static string CacheName(string path, string canonical)
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

    [GeneratedRegex(
        @"^(?:https://)?github\.com/(?<owner>[A-Za-z0-9_.-]+)/(?<repo>[A-Za-z0-9_.-]+?)(?:\.git)?(?:@(?<ref>.+))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GitHubSource();

    private readonly record struct Parsed(string Body, string? Reference, Match? GitHub, bool File, bool Folder);
}
