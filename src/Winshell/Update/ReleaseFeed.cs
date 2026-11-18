using System.Buffers;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Winshell;

internal static partial class ReleaseSources
{
    private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdefABCDEF");

    private const string GitHubBase = "https://github.com/enpaze/winshell/releases";

    private const string RejectedBase =
        "WINSHELL_RELEASE_BASE must be https://github.com/enpaze/winshell/releases or an http://127.0.0.1 test server.";

    internal static IReleaseFeed Create(string? releaseBase, HttpClient? http = null)
    {
        var client = http ?? new HttpClient();
        if (string.IsNullOrWhiteSpace(releaseBase))
            return new GitHubReleaseFeed(client, null);

        var trimmed = releaseBase.Trim().TrimEnd('/');
        if (trimmed == GitHubBase)
            return new GitHubReleaseFeed(client, null);

        if (!LoopbackBase().IsMatch(trimmed))
            throw new InvalidOperationException(RejectedBase);

        return new GitHubReleaseFeed(client, new Uri(trimmed + "/"));
    }

    internal static string ZipName(string version)
    {
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        var bare = plus < 0 ? version : version[..plus];
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "win-x64",
            Architecture.Arm64 => "win-arm64",
            _ => throw new InvalidOperationException(
                "Winshell publishes win-x64 and win-arm64. This PC is '" + RuntimeInformation.OSArchitecture + "'.")
        };
        return "winshell-" + bare + "-" + arch + ".zip";
    }

    internal static bool IsCurrent(UpdateChannel channel, string informational, ReleaseListing listing)
    {
        if (channel == UpdateChannel.Stable)
        {
            var plus = informational.IndexOf('+', StringComparison.Ordinal);
            var running = plus < 0 ? informational : informational[..plus];
            return string.Equals(running, listing.Version, StringComparison.Ordinal);
        }

        var mark = informational.IndexOf('+', StringComparison.Ordinal);
        if (mark < 0 || mark == informational.Length - 1 || listing.Commit.Length == 0)
            return false;

        return informational[(mark + 1)..].Equals(listing.Commit, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ChecksumMatches(string sums, string name, ReadOnlySpan<byte> zip)
    {
        var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(zip));
        foreach (var raw in sums.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length != name.Length + 66 || line[64] != ' ' || line[65] is not (' ' or '*'))
                continue;
            if (!line.AsSpan(66).Equals(name, StringComparison.Ordinal))
                continue;
            var hash = line.AsSpan(0, 64);
            return hash.IndexOfAnyExcept(HexDigits) < 0
                   && hash.Equals(expected, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    [GeneratedRegex("^http://(127\\.0\\.0\\.1|localhost)(:[0-9]{1,5})?(/[A-Za-z0-9._~/-]*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex LoopbackBase();
}

internal sealed class GitHubReleaseFeed : IReleaseFeed
{
    private const string LatestUrl = "https://api.github.com/repos/enpaze/winshell/releases/latest";

    private const string DevUrl = "https://api.github.com/repos/enpaze/winshell/releases?per_page=30";

    private readonly HttpClient _http;

    private readonly Uri? _loopback;

    internal GitHubReleaseFeed(HttpClient http, Uri? loopback)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        _loopback = loopback;
    }

    public ReleaseListing Latest(UpdateChannel channel)
    {
        if (channel is not UpdateChannel.Stable and not UpdateChannel.Dev)
            throw new ArgumentOutOfRangeException(nameof(channel));

        var dev = channel == UpdateChannel.Dev;
        ReleaseDocument release;
        try
        {
            release = ReleaseDocument.Read(GetText(ReleaseUrl(channel), document: true, dev), dev);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(dev
                ? "The dev release could not be read."
                : "The published release could not be read.");
        }

        ReleaseVersion listed;
        try
        {
            listed = ReleaseVersion.Read(GetText(Asset(release, "version.json"), document: false, dev), dev);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(dev
                ? "The dev release's version.json has no version."
                : "The published release's version.json has no version.");
        }

        var archive = ReleaseSources.ZipName(listed.Version);
        return new ReleaseListing(listed.Version, listed.Commit, Asset(release, archive), Asset(release, "SHA256SUMS.txt"));
    }

    public byte[] Download(string url)
    {
        return GetBytes(url, document: false, dev: false);
    }

    private string ReleaseUrl(UpdateChannel channel)
    {
        if (_loopback is null)
            return channel == UpdateChannel.Dev ? DevUrl : LatestUrl;

        return new Uri(_loopback, channel == UpdateChannel.Dev ? "list" : "latest").AbsoluteUri;
    }

    private string GetText(string url, bool document, bool dev)
    {
        return Encoding.UTF8.GetString(GetBytes(url, document, dev));
    }

    private byte[] GetBytes(string url, bool document, bool dev)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("The release feed refused a download that is not https.");

        if (uri.Scheme != Uri.UriSchemeHttps
            && !(_loopback is not null
                 && uri.Scheme == Uri.UriSchemeHttp
                 && (uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                     || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException("The release feed refused a download that is not https.");

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("User-Agent", "winshell");
        request.Headers.TryAddWithoutValidation("Accept", document ? "application/vnd.github+json" : "application/octet-stream");

        HttpResponseMessage response;
        try
        {
            response = _http.Send(request);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException("The published release could not be read: " + exception.Message);
        }

        using (response)
        {
            var code = (int)response.StatusCode;
            if (code is 403 or 429
                && response.Headers.TryGetValues("x-ratelimit-remaining", out var remaining)
                && remaining.Contains("0"))
            {
                var reset = "Try again later.";
                if (response.Headers.TryGetValues("x-ratelimit-reset", out var values)
                    && long.TryParse(values.FirstOrDefault(), CultureInfo.InvariantCulture, out var seconds)
                    && seconds is >= 0 and <= 253402300799)
                {
                    reset = "Try again after " + DateTimeOffset.FromUnixTimeSeconds(seconds)
                        .ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) + ".";
                }

                throw new InvalidOperationException("The GitHub API rate limit is used up. " + reset);
            }

            if (code == (int)HttpStatusCode.NotFound && document)
            {
                throw new InvalidOperationException(dev
                    ? "No dev release was found."
                    : "No published release was found.");
            }

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("The published release could not be read (HTTP " + code + ").");

            using var stream = response.Content.ReadAsStream();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
    }

    private static string Asset(ReleaseDocument release, string name)
    {
        if (!release.Assets.TryGetValue(name, out var url))
            throw new InvalidOperationException("The published release has no " + name + ".");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidOperationException("The published release has an unsupported download for " + name + ".");

        return uri.AbsoluteUri;
    }
}

internal sealed partial class ReleaseDocument
{
    private ReleaseDocument(Dictionary<string, string> assets)
    {
        Assets = assets;
    }

    internal Dictionary<string, string> Assets { get; }

    internal static ReleaseDocument Read(string json, bool dev)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        var root = dev ? NewestDev(document.RootElement) : document.RootElement;
        if (!dev && (Flag(root, "draft") || Flag(root, "prerelease")))
            throw new InvalidOperationException("The latest release is not a published release.");

        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("The published release has no assets.");

        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var asset in assets.EnumerateArray())
        {
            var name = Text(asset, "name");
            var url = Text(asset, "browser_download_url");
            if (name is null || url is null)
                continue;
            found[name] = url;
        }

        if (!found.ContainsKey("version.json"))
            throw new InvalidOperationException("The published release has no version.json.");

        if (!found.ContainsKey("SHA256SUMS.txt"))
            throw new InvalidOperationException("The published release has no SHA256SUMS.txt.");

        return new ReleaseDocument(found);
    }

    private static JsonElement NewestDev(JsonElement releases)
    {
        if (releases.ValueKind != JsonValueKind.Array)
            throw new JsonException();

        JsonElement? newest = null;
        var newestAt = DateTimeOffset.MinValue;
        foreach (var release in releases.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object || Flag(release, "draft") || !Flag(release, "prerelease"))
                continue;
            var tag = Text(release, "tag_name");
            var published = Text(release, "published_at");
            if (tag is null || !DevTag().IsMatch(tag) || published is null
                || !DateTimeOffset.TryParse(published, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
                continue;
            if (newest is null || at > newestAt)
            {
                newest = release;
                newestAt = at;
            }
        }

        return newest ?? throw new InvalidOperationException("No dev release was found.");
    }

    [GeneratedRegex("^dev-[0-9a-f]{7}$", RegexOptions.CultureInvariant)]
    private static partial Regex DevTag();

    private static bool Flag(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    }

    internal static string? Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            return null;

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}

internal readonly partial record struct ReleaseVersion(string Version, string Commit)
{
    private static readonly SearchValues<char> VersionBreaks = SearchValues.Create("\"\r\n");

    internal static ReleaseVersion Read(string json, bool dev)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var version = ReleaseDocument.Text(root, "version");
        var commit = ReleaseDocument.Text(root, "commit");
        var date = ReleaseDocument.Text(root, "date");
        if (version is null || version.Length > 64 || version.AsSpan().IndexOfAny(VersionBreaks) >= 0)
        {
            throw new InvalidOperationException(dev
                ? "The dev release's version.json has no version."
                : "The published release's version.json has no version.");
        }

        if (!dev && !VersionShape().IsMatch(version))
        {
            throw new InvalidOperationException("The published release's version.json has no version.");
        }

        if (commit is null || !CommitShape().IsMatch(commit))
        {
            throw new InvalidOperationException(dev
                ? "The dev release's version.json has no commit."
                : "The published release's version.json has no commit.");
        }

        if (date is null
            || !DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            || parsed.Offset != TimeSpan.Zero)
        {
            throw new InvalidOperationException(dev
                ? "The dev release's version.json has no date."
                : "The published release's version.json has no date.");
        }

        return new ReleaseVersion(version, commit.ToLowerInvariant());
    }

    [GeneratedRegex("^\\d+\\.\\d+\\.\\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionShape();

    [GeneratedRegex("^[0-9a-fA-F]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex CommitShape();
}
