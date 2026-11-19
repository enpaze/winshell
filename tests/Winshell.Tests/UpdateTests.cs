using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Winshell.Commands;

namespace Winshell.Tests;

public class UpdateCommandTests
{
    private const string CommitA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string CommitB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public async Task Missing_channel_file_uses_stable_and_writes_nothing()
    {
        using var temp = new TempFolder();
        var feed = new FakeFeed(Listing("1.2.3", CommitA), [], "");
        var (code, output, error) = await Run(temp, feed, "1.2.3+aaaaaaaa", requested: null, installed: true);

        Assert.Equal(0, code);
        Assert.Equal("Winshell is up to date.", output.Trim());
        Assert.Equal("", error);
        Assert.Equal(UpdateChannel.Stable, feed.Seen);
        Assert.Equal(0, feed.Downloads);
        Assert.False(File.Exists(Path.Combine(Config(temp), "update.toml")));
    }

    [Fact]
    public async Task Channel_flag_on_a_current_build_is_remembered_and_the_next_call_uses_it()
    {
        using var temp = new TempFolder();
        var feed = new FakeFeed(Listing("1.2.3-dev.4", CommitA), [], "");
        var (code, output, _) = await Run(temp, feed, "0.0.0-dev.1+" + CommitA, requested: "dev", installed: true);

        Assert.Equal(0, code);
        Assert.Equal("Winshell is up to date.", output.Trim());
        Assert.Equal("channel = \"dev\"\n", File.ReadAllText(Path.Combine(Config(temp), "update.toml")));
        Assert.Equal(0, feed.Downloads);

        feed.Seen = null;
        var again = await Run(temp, feed, "0.0.0-dev.1+" + CommitA.ToUpperInvariant(), requested: null, installed: true);
        Assert.Equal(0, again.Code);
        Assert.Equal(UpdateChannel.Dev, feed.Seen);
        Assert.Equal(0, feed.Downloads);
        Assert.Equal("channel = \"dev\"\n", File.ReadAllText(Path.Combine(Config(temp), "update.toml")));
    }

    [Fact]
    public async Task Saved_channel_other_than_stable_or_dev_fails_before_any_download()
    {
        using var temp = new TempFolder();
        Directory.CreateDirectory(Config(temp));
        var path = Path.Combine(Config(temp), "update.toml");
        File.WriteAllText(path, "channel = \"nightly\"\n");
        var feed = new ExplodingFeed();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Run(temp, feed, "1.2.3", requested: null, installed: true));

        Assert.Equal("The saved update channel is not stable or dev.", error.Message);
        Assert.Equal("channel = \"nightly\"\n", File.ReadAllText(path));
    }

    [Fact]
    public async Task A_channel_file_with_an_extra_key_is_rejected()
    {
        using var temp = new TempFolder();
        Directory.CreateDirectory(Config(temp));
        File.WriteAllText(Path.Combine(Config(temp), "update.toml"), "channel = \"dev\"\nother = 1\n");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Run(temp, new ExplodingFeed(), "1.2.3", requested: null, installed: true));

        Assert.Equal("The saved update channel is not stable or dev.", error.Message);
    }

    [Fact]
    public async Task Uppercase_saved_channel_selects_dev()
    {
        using var temp = new TempFolder();
        Directory.CreateDirectory(Config(temp));
        File.WriteAllText(Path.Combine(Config(temp), "update.toml"), "channel = \"DEV\"\n");
        var feed = new FakeFeed(Listing("1.2.3-dev.4", CommitA), [], "");

        var (code, _, _) = await Run(temp, feed, "0.0.0+" + CommitA, requested: null, installed: true);

        Assert.Equal(0, code);
        Assert.Equal(UpdateChannel.Dev, feed.Seen);
        Assert.Equal(0, feed.Downloads);
    }

    [Fact]
    public async Task Other_channel_flag_fails_and_writes_nothing()
    {
        using var temp = new TempFolder();
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            Run(temp, new ExplodingFeed(), "1.2.3", requested: "other", installed: true));

        Assert.Equal("winshell update --channel needs stable or dev.", error.Message);
        Assert.False(File.Exists(Path.Combine(Config(temp), "update.toml")));
    }

    [Fact]
    public async Task Not_installed_prints_the_sentence_and_downloads_nothing()
    {
        using var temp = new TempFolder();
        var (code, output, error) = await Run(temp, new ExplodingFeed(), "1.2.3", requested: "dev", installed: false);

        Assert.Equal(1, code);
        Assert.Equal("", output);
        Assert.Equal("Winshell is not installed. Run the install script.", error.Trim());
        Assert.False(File.Exists(Path.Combine(Config(temp), "update.toml")));
        Assert.False(Directory.Exists(Path.Combine(Install(temp), "bin.new")));
    }

    [Fact]
    public async Task Already_current_stable_does_not_extract()
    {
        using var temp = new TempFolder();
        var bin = Bin(temp);
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "keep.txt"), "same");
        Directory.CreateDirectory(Path.Combine(Install(temp), "bin.new"));
        File.WriteAllText(Path.Combine(Install(temp), "bin.new", "stale.txt"), "stale");
        var feed = new FakeFeed(Listing("1.2.3", CommitA), [1, 2, 3], "nope");

        var (code, output, _) = await Run(temp, feed, "1.2.3+deadbeef", requested: "stable", installed: true);

        Assert.Equal(0, code);
        Assert.Equal("Winshell is up to date.", output.Trim());
        Assert.Equal(0, feed.Downloads);
        Assert.Equal("same", File.ReadAllText(Path.Combine(bin, "keep.txt")));
        Assert.False(Directory.Exists(Path.Combine(Install(temp), "bin.new")));
        Assert.Equal("channel = \"stable\"\n", File.ReadAllText(Path.Combine(Config(temp), "update.toml")));
    }

    [Fact]
    public async Task A_newer_stable_build_replaces_the_files_and_reports_that_version()
    {
        using var temp = new TempFolder();
        var bin = Bin(temp);
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "keep.txt"), "old");
        var name = ReleaseSources.ZipName("9.9.9");
        var zip = UpdateStub.Archive("NEW");
        var feed = new FakeFeed(Listing("9.9.9", CommitA), zip, Sums(name, zip));

        var (code, output, error) = await Run(temp, feed, "1.0.0", requested: "stable", installed: true);

        Assert.Equal(0, code);
        Assert.Equal("", error);
        Assert.Equal("Winshell is updated to 9.9.9.", output.Trim());
        Assert.False(File.Exists(Path.Combine(bin, "keep.txt")));
        Assert.Equal(File.ReadAllBytes(Path.Combine(UpdateStub.Build("NEW"), "winshellw.exe")), File.ReadAllBytes(Path.Combine(bin, "winshellw.exe")));
        Assert.Equal("channel = \"stable\"\n", File.ReadAllText(Path.Combine(Config(temp), "update.toml")));
        Assert.False(Directory.Exists(Path.Combine(Install(temp), "bin.new")));
    }

    [Fact]
    public async Task A_bad_checksum_replaces_nothing_and_does_not_write_the_channel()
    {
        using var temp = new TempFolder();
        var bin = Bin(temp);
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "keep.txt"), "old");
        var name = ReleaseSources.ZipName("9.9.9");
        var feed = new FakeFeed(Listing("9.9.9", CommitA), [9, 9, 9], new string('a', 64) + "  " + name + "\n");

        var (code, output, error) = await Run(temp, feed, "1.0.0", requested: "dev", installed: true);

        Assert.Equal(1, code);
        Assert.Equal("", output);
        Assert.Equal(
            "Refusing the update: the SHA-256 of " + name + " does not match SHA256SUMS.txt. The release may still be uploading. Try again in a minute.",
            error.Trim());
        Assert.Equal("old", File.ReadAllText(Path.Combine(bin, "keep.txt")));
        Assert.False(File.Exists(Path.Combine(Config(temp), "update.toml")));
        Assert.False(Directory.Exists(Path.Combine(Install(temp), "bin.new")));
    }

    [Fact]
    public async Task A_space_star_checksum_line_is_accepted()
    {
        using var temp = new TempFolder();
        Directory.CreateDirectory(Bin(temp));
        var name = ReleaseSources.ZipName("3.0.0");
        var zip = UpdateStub.Archive("NEW");
        var hash = Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant();
        var feed = new FakeFeed(Listing("3.0.0", CommitA), zip, hash + " *" + name + "\n");

        var (code, output, _) = await Run(temp, feed, "1.0.0", requested: null, installed: true);

        Assert.Equal(0, code);
        Assert.Equal("Winshell is updated to 3.0.0.", output.Trim());
        Assert.False(File.Exists(Path.Combine(Config(temp), "update.toml")));
    }

    [Fact]
    public async Task The_same_dev_commit_is_current_and_a_different_one_updates()
    {
        using var temp = new TempFolder();
        var bin = Bin(temp);
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "keep.txt"), "old");
        Directory.CreateDirectory(Config(temp));
        File.WriteAllText(Path.Combine(Config(temp), "update.toml"), "channel = \"dev\"\n");
        var same = new FakeFeed(Listing("1.2.3-dev.4", CommitA), [1], "nope");
        var (code, _, _) = await Run(temp, same, "9.9.9+" + CommitA, requested: null, installed: true);
        Assert.Equal(0, code);
        Assert.Equal(0, same.Downloads);
        Assert.Equal("old", File.ReadAllText(Path.Combine(bin, "keep.txt")));

        var name = ReleaseSources.ZipName("1.2.3-dev.5+" + CommitB[..7]);
        var zip = UpdateStub.Archive("NEW");
        var next = new FakeFeed(Listing("1.2.3-dev.5+" + CommitB[..7], CommitB), zip, Sums(name, zip));
        var updated = await Run(temp, next, "9.9.9+" + CommitA, requested: "dev", installed: true);
        Assert.Equal(0, updated.Code);
        Assert.Equal("Winshell is updated to 1.2.3-dev.5+" + CommitB[..7] + ".", updated.Output.Trim());
        Assert.False(File.Exists(Path.Combine(bin, "keep.txt")));
        Assert.Equal("channel = \"dev\"\n", File.ReadAllText(Path.Combine(Config(temp), "update.toml")));
    }

    [Fact]
    public async Task Switching_to_stable_installs_that_zip_when_its_version_is_older()
    {
        using var temp = new TempFolder();
        Directory.CreateDirectory(Bin(temp));
        var name = ReleaseSources.ZipName("1.0.0");
        var zip = UpdateStub.Archive("OLD");
        var feed = new FakeFeed(Listing("1.0.0", CommitA), zip, Sums(name, zip));

        var (code, output, _) = await Run(temp, feed, "9.0.0+" + CommitB, requested: "stable", installed: true);

        Assert.Equal(0, code);
        Assert.Equal("Winshell is updated to 1.0.0.", output.Trim());
        Assert.Equal(File.ReadAllBytes(Path.Combine(UpdateStub.Build("OLD"), "winshellw.exe")), File.ReadAllBytes(Path.Combine(Bin(temp), "winshellw.exe")));
        Assert.Equal("channel = \"stable\"\n", File.ReadAllText(Path.Combine(Config(temp), "update.toml")));
    }

    [Fact]
    public async Task An_empty_dev_suffix_is_not_current()
    {
        using var temp = new TempFolder();
        Directory.CreateDirectory(Bin(temp));
        Directory.CreateDirectory(Config(temp));
        File.WriteAllText(Path.Combine(Config(temp), "update.toml"), "channel = \"dev\"\n");
        var version = "1.2.3-dev.2+" + CommitB[..7];
        var name = ReleaseSources.ZipName(version);
        var zip = UpdateStub.Archive("NEW");
        var feed = new FakeFeed(Listing(version, CommitB), zip, Sums(name, zip));

        var (code, output, _) = await Run(temp, feed, "1.2.3-dev.2+", requested: null, installed: true);

        Assert.Equal(0, code);
        Assert.Equal("Winshell is updated to " + version + ".", output.Trim());
        Assert.True(feed.Downloads > 0);
    }

    [Fact]
    public async Task A_bad_archive_deletes_the_stage_and_leaves_the_install()
    {
        using var temp = new TempFolder();
        var bin = Bin(temp);
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "keep.txt"), "old");
        var name = ReleaseSources.ZipName("2.0.0");
        var zip = ZipOf(("note.txt", "hello"u8.ToArray()));
        var feed = new FakeFeed(Listing("2.0.0", CommitA), zip, Sums(name, zip));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Run(temp, feed, "1.0.0", requested: "stable", installed: true));

        Assert.Equal("The release archive has no winshell.exe and winshellw.exe.", error.Message);
        Assert.Equal("old", File.ReadAllText(Path.Combine(bin, "keep.txt")));
        Assert.False(Directory.Exists(Path.Combine(Install(temp), "bin.new")));
        Assert.False(File.Exists(Path.Combine(Config(temp), "update.toml")));
    }

    [Fact]
    public async Task A_zip_that_escapes_the_stage_changes_nothing()
    {
        using var temp = new TempFolder();
        var bin = Bin(temp);
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "keep.txt"), "old");
        var name = ReleaseSources.ZipName("2.0.0");
        var zip = ZipOf(("../outside.txt", "nope"u8.ToArray()));
        var feed = new FakeFeed(Listing("2.0.0", CommitA), zip, Sums(name, zip));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Run(temp, feed, "1.0.0", requested: null, installed: true));

        Assert.Equal("The release archive is not a winshell folder.", error.Message);
        Assert.Equal("old", File.ReadAllText(Path.Combine(bin, "keep.txt")));
        Assert.False(File.Exists(Path.Combine(temp.Path, "outside.txt")));
        Assert.False(Directory.Exists(Path.Combine(Install(temp), "bin.new")));
    }

    [Fact]
    public async Task A_shell_that_does_not_stop_leaves_the_install_and_exits_1()
    {
        using var temp = new TempFolder();
        var bin = Bin(temp);
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "keep.txt"), "old");
        var name = ReleaseSources.ZipName("2.0.0");
        var zip = UpdateStub.Archive("NEW");
        var feed = new FakeFeed(Listing("2.0.0", CommitA), zip, Sums(name, zip));

        var (code, output, error) = await Run(temp, feed, "1.0.0", requested: "dev", installed: true, stop: () => Task.FromResult(false));

        Assert.Equal(1, code);
        Assert.Equal("", output);
        Assert.Equal("", error);
        Assert.Equal("old", File.ReadAllText(Path.Combine(bin, "keep.txt")));
        Assert.False(Directory.Exists(Path.Combine(Install(temp), "bin.new")));
        Assert.False(File.Exists(Path.Combine(Config(temp), "update.toml")));
    }

    [Fact]
    public void Update_help_and_a_bad_channel_do_not_write_or_download()
    {
        using var temp = new TempFolder();
        var help = WinshellCli.Run(["update", "-h"], ("WINSHELL_SHELLS_DIR", temp.Path));
        help.AssertExitCode(0);
        Assert.Contains("Installs the newest build of the saved channel.", help.Output, StringComparison.Ordinal);
        help.AssertNoError();
        Assert.False(File.Exists(Path.Combine(temp.Path, "update.toml")));

        var version = WinshellCli.Run(["update", "--version"], ("WINSHELL_SHELLS_DIR", temp.Path));
        version.AssertExitCode(0);
        Assert.StartsWith("winshell ", version.Output, StringComparison.Ordinal);
        version.AssertNoError();

        var missing = WinshellCli.Run(["update", "--channel"], ("WINSHELL_SHELLS_DIR", temp.Path));
        missing.AssertExitCode(2);
        Assert.Equal("winshell update --channel needs stable or dev.", missing.Error.Trim());

        var other = WinshellCli.Run(["update", "--channel", "other"], ("WINSHELL_SHELLS_DIR", temp.Path));
        other.AssertExitCode(2);
        Assert.Equal("winshell update --channel needs stable or dev.", other.Error.Trim());
        Assert.False(File.Exists(Path.Combine(temp.Path, "update.toml")));

        var spare = WinshellCli.Run(["update", "extra"], ("WINSHELL_SHELLS_DIR", temp.Path));
        spare.AssertExitCode(2);
    }

    [Fact]
    public void The_real_command_reports_not_installed_and_a_bad_saved_channel()
    {
        using var temp = new TempFolder();
        var missing = WinshellCli.Run(["update"], ("WINSHELL_SHELLS_DIR", temp.Path));
        missing.AssertExitCode(1);
        Assert.Equal("Winshell is not installed. Run the install script.", missing.Error.Trim());
        Assert.Equal("", missing.Output.Trim());
        Assert.False(File.Exists(Path.Combine(temp.Path, "update.toml")));

        var path = Path.Combine(temp.Path, "update.toml");
        File.WriteAllText(path, "channel = \"nope\"\n");
        var bad = WinshellCli.Run(["update"], ("WINSHELL_SHELLS_DIR", temp.Path));
        Assert.Equal(1, bad.Code);
        Assert.Contains("The saved update channel is not stable or dev.", bad.Error, StringComparison.Ordinal);
        Assert.Equal("channel = \"nope\"\n", File.ReadAllText(path));
    }

    private static async Task<(int Code, string Output, string Error)> Run(
        TempFolder temp,
        IReleaseFeed feed,
        string informational,
        string? requested,
        bool installed,
        Func<Task<bool>>? stop = null)
    {
        var install = Install(temp);
        var executable = installed
            ? Path.Combine(install, "bin", "winshell.exe")
            : Path.Combine(install, "winshell.exe");
        await using var output = new StringWriter();
        await using var error = new StringWriter();
        var code = await UpdateCommand.RunAsync(
            feed,
            install,
            Config(temp),
            executable,
            informational,
            requested,
            output,
            error,
            stop);
        UpdateStub.Release(install);
        return (code, output.ToString(), error.ToString());
    }

    private static string Install(TempFolder temp) => Path.Combine(temp.Path, "install");

    private static string Config(TempFolder temp) => Path.Combine(temp.Path, "config");

    private static string Bin(TempFolder temp) => Path.Combine(Install(temp), "bin");

    private static ReleaseListing Listing(string version, string commit) =>
        new(version, commit, "https://example.invalid/archive", "https://example.invalid/SHA256SUMS.txt");

    private static string Sums(string name, byte[] zip) =>
        Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant() + "  " + name + "\n";

    private static byte[] ZipOf(params (string Name, byte[] Bytes)[] files)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in files)
            {
                var entry = archive.CreateEntry(name);
                using var body = entry.Open();
                body.Write(bytes);
            }
        }

        return stream.ToArray();
    }

    private sealed class FakeFeed : IReleaseFeed
    {
        private readonly ReleaseListing _listing;
        private readonly byte[] _zip;
        private readonly byte[] _sums;

        internal FakeFeed(ReleaseListing listing, byte[] zip, string sums)
        {
            _listing = listing;
            _zip = zip;
            _sums = Encoding.UTF8.GetBytes(sums);
        }

        internal UpdateChannel? Seen { get; set; }

        internal int Downloads { get; private set; }

        public ReleaseListing Latest(UpdateChannel channel)
        {
            Seen = channel;
            return _listing;
        }

        public byte[] Download(string url)
        {
            Downloads++;
            if (url == _listing.ArchiveUrl)
                return _zip;
            if (url == _listing.ChecksumsUrl)
                return _sums;
            throw new InvalidOperationException(url);
        }
    }

    private sealed class ExplodingFeed : IReleaseFeed
    {
        public ReleaseListing Latest(UpdateChannel channel) => throw new InvalidOperationException("downloaded");

        public byte[] Download(string url) => throw new InvalidOperationException("downloaded");
    }
}

public class ReleaseFeedTests
{
    [Fact]
    public void A_disallowed_release_base_is_rejected()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ReleaseSources.Create("https://example.com/winshell"));
        Assert.Equal(
            "WINSHELL_RELEASE_BASE must be https://github.com/enpaze/winshell/releases or an http://127.0.0.1 test server.",
            error.Message);
    }

    [Fact]
    public void The_github_releases_page_is_allowed_and_does_not_download()
    {
        using var http = new HttpClient(new Script(_ => throw new InvalidOperationException("downloaded")));
        var feed = ReleaseSources.Create("https://github.com/enpaze/winshell/releases", http);
        Assert.NotNull(feed);
    }

    [Fact]
    public void Stable_rejects_a_draft_and_a_prerelease()
    {
        var draft = Assert.Throws<InvalidOperationException>(() => Latest(false, """{"draft":true,"prerelease":false,"assets":[]}"""));
        Assert.Equal("The latest release is not a published release.", draft.Message);
        var preview = Assert.Throws<InvalidOperationException>(() => Latest(false, """{"draft":false,"prerelease":true,"assets":[]}"""));
        Assert.Equal("The latest release is not a published release.", preview.Message);
    }

    [Fact]
    public void Dev_reads_the_newest_published_dev_prerelease()
    {
        var zip = ReleaseSources.ZipName("1.2.3-dev.4");
        var list = "["
                   + DevJson("dev-1111111", "2026-11-18T05:00:00Z", zip, draft: true)
                   + "," + DevJson("v9.9.9", "2026-11-18T04:00:00Z", zip)
                   + "," + DevJson("dev-2222222", "2026-11-18T01:00:00Z", zip)
                   + "," + DevJson("dev-3333333", "2026-11-18T03:00:00Z", zip)
                   + "," + DevJson("dev-4444444", "2026-11-18T06:00:00Z", zip, prerelease: false)
                   + "]";
        var version = VersionJson("1.2.3-dev.4", "ABCDEFABCdefabcdefabcdefabcdefabcdefabcd", "2026-11-18T01:02:03Z");
        var seen = new List<string>();
        var listing = Read(true, request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            seen.Add(url);
            return Text(HttpStatusCode.OK, url.EndsWith("/list", StringComparison.Ordinal) ? list : version);
        });

        Assert.Equal("1.2.3-dev.4", listing.Version);
        Assert.Equal("abcdefabcdefabcdefabcdefabcdefabcdefabcd", listing.Commit);
        Assert.Equal("https://example.invalid/dev-3333333/" + zip, listing.ArchiveUrl);
        Assert.Equal(["http://127.0.0.1:9/releases/list", "https://example.invalid/dev-3333333/version.json"], seen);
    }

    [Fact]
    public void Dev_with_no_published_dev_prerelease_is_not_found()
    {
        var zip = ReleaseSources.ZipName("1.2.3-dev.4");
        var list = "[" + DevJson("dev-1111111", "2026-11-18T05:00:00Z", zip, draft: true) + "]";
        var error = Assert.Throws<InvalidOperationException>(() => Latest(true, list));
        Assert.Equal("No dev release was found.", error.Message);
    }

    [Fact]
    public void A_stable_release_reads_version_commit_and_asset_names()
    {
        var version = VersionJson("1.2.3", "abcdefabcdefabcdefabcdefabcdefabcdefabcd", "2026-11-18T01:02:03+00:00");
        var zip = ReleaseSources.ZipName("1.2.3");
        string? agent = null;
        string? accept = null;
        var listing = Read(false, request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url.EndsWith("/latest", StringComparison.Ordinal))
            {
                agent = request.Headers.UserAgent.ToString();
                accept = request.Headers.Accept.ToString();
                return Text(HttpStatusCode.OK, ReleaseJson(dev: false, zip));
            }

            return Text(HttpStatusCode.OK, version);
        });

        Assert.Equal("1.2.3", listing.Version);
        Assert.Equal("abcdefabcdefabcdefabcdefabcdefabcdefabcd", listing.Commit);
        Assert.Contains("winshell", agent, StringComparison.Ordinal);
        Assert.Contains("application/vnd.github+json", accept, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, "No published release was found.")]
    [InlineData(true, "No dev release was found.")]
    public void A_missing_release_names_the_channel(bool dev, string message)
    {
        var error = Assert.Throws<InvalidOperationException>(() => Latest(dev, status: HttpStatusCode.NotFound));
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void The_rate_limit_sentence_includes_the_reset_time()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Read(false, _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
            response.Headers.TryAddWithoutValidation("x-ratelimit-remaining", "0");
            response.Headers.TryAddWithoutValidation("x-ratelimit-reset", "0");
            response.Content = new ByteArrayContent([]);
            return response;
        }));
        Assert.Equal("The GitHub API rate limit is used up. Try again after 1970-01-01 00:00:00 UTC.", error.Message);
    }

    [Fact]
    public void Version_json_without_a_utc_date_is_rejected()
    {
        var zip = ReleaseSources.ZipName("1.2.3");
        var error = Assert.Throws<InvalidOperationException>(() => Read(false, request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            var body = url.EndsWith("/latest", StringComparison.Ordinal)
                ? ReleaseJson(false, zip)
                : VersionJson("1.2.3", "abcdefabcdefabcdefabcdefabcdefabcdefabcd", "2026-11-18T01:02:03+01:00");
            return Text(HttpStatusCode.OK, body);
        }));
        Assert.Equal("The published release's version.json has no date.", error.Message);
    }

    [Fact]
    public void An_http_asset_on_the_github_feed_is_refused()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Read(false, _ => Text(HttpStatusCode.OK, """
            {"draft":false,"prerelease":false,"assets":[
              {"name":"version.json","browser_download_url":"http://example.com/version.json"},
              {"name":"SHA256SUMS.txt","browser_download_url":"https://example.com/SHA256SUMS.txt"}]}
            """)));
        Assert.Equal("The release feed refused a download that is not https.", error.Message);
    }

    private static ReleaseListing Latest(bool dev, string json = "{}", HttpStatusCode status = HttpStatusCode.OK) =>
        Read(dev, _ => Text(status, json));

    private static ReleaseListing Read(bool dev, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        using var http = new HttpClient(new Script(respond));
        var feed = ReleaseSources.Create("http://127.0.0.1:9/releases", http);
        return feed.Latest(dev ? UpdateChannel.Dev : UpdateChannel.Stable);
    }

    private static string ReleaseJson(bool dev, string zip) =>
        "{\"draft\":false,\"prerelease\":" + (dev ? "true" : "false") + ",\"assets\":["
        + Asset("version.json") + ","
        + Asset("SHA256SUMS.txt") + ","
        + Asset(zip) + "]}";

    private static string DevJson(string tag, string published, string zip, bool draft = false, bool prerelease = true) =>
        "{\"tag_name\":\"" + tag + "\",\"published_at\":\"" + published + "\",\"draft\":" + (draft ? "true" : "false")
        + ",\"prerelease\":" + (prerelease ? "true" : "false") + ",\"assets\":["
        + Asset(tag + "/version.json", "version.json") + ","
        + Asset(tag + "/SHA256SUMS.txt", "SHA256SUMS.txt") + ","
        + Asset(tag + "/" + zip, zip) + "]}";

    private static string Asset(string name) => Asset(name, name);

    private static string Asset(string path, string name) =>
        "{\"name\":\"" + name + "\",\"browser_download_url\":\"https://example.invalid/" + path + "\"}";

    private static string VersionJson(string version, string commit, string date) =>
        "{\"version\":\"" + version + "\",\"commit\":\"" + commit + "\",\"date\":\"" + date + "\"}";

    private static HttpResponseMessage Text(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private sealed class Script : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        internal Script(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _respond(request);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Send(request, cancellationToken));
    }
}

public class ReleaseSwapTests
{
    [Fact]
    public async Task A_running_process_is_replaced_by_the_new_build_with_no_window()
    {
        using var temp = new TempFolder();
        var install = Path.Combine(temp.Path, "install");
        var bin = Path.Combine(install, "bin");
        var status = Path.Combine(temp.Path, "status.txt");
        UpdateStub.Install("OLD", bin);
        Process? sleeper = null;
        try
        {
            sleeper = UpdateStub.Start(Path.Combine(bin, "winshellw.exe"), status, "--hold");
            UpdateStub.WaitUntil(() => File.Exists(status) && File.ReadAllText(status).Contains("OLD\t", StringComparison.Ordinal), sleeper, "old build");
            var name = ReleaseSources.ZipName("9.9.9");
            var zip = UpdateStub.Archive("NEW");
            var feed = new ListedFeed(new ReleaseListing("9.9.9", new string('c', 40), "https://example.invalid/archive", "https://example.invalid/SHA256SUMS.txt"), zip, name);
            var result = await ReleaseUpdate.ApplyAsync(
                feed,
                UpdateChannel.Stable,
                install,
                Path.Combine(bin, "winshell.exe"),
                "0.0.1",
                beforeWait: () =>
                {
                    sleeper.Kill(entireProcessTree: true);
                    sleeper.WaitForExit();
                });

            var updated = Assert.IsType<UpdateResult.Updated>(result);
            Assert.Equal("9.9.9", updated.Version);
            Assert.True(updated.Restarted);
            Assert.Equal(
                File.ReadAllBytes(Path.Combine(UpdateStub.Build("NEW"), "winshellw.exe")),
                File.ReadAllBytes(Path.Combine(bin, "winshellw.exe")));
            UpdateStub.WaitUntil(() => Status(status, "NEW") is not null, null, "new build");
            var line = Status(status, "NEW");
            Assert.NotNull(line);
            Assert.Equal("0", line![2]);
            Assert.Contains("--hold", line[3], StringComparison.Ordinal);
            var running = UpdateStub.Find(Path.Combine(bin, "winshellw.exe"));
            Assert.NotNull(running);
            running!.Refresh();
            Assert.Equal(IntPtr.Zero, running.MainWindowHandle);
        }
        finally
        {
            UpdateStub.Release(install);
            sleeper?.Dispose();
        }
    }

    [Fact]
    public void The_staged_helper_starts_the_new_build_with_no_window()
    {
        using var temp = new TempFolder();
        var install = Path.Combine(temp.Path, "install");
        var status = Path.Combine(temp.Path, "status.txt");
        UpdateStub.Install("OLD", Path.Combine(install, "bin"));
        UpdateStub.Install("NEW", Path.Combine(install, "bin.new"));
        var plan = Path.Combine(install, "swap", "plan.json");
        Directory.CreateDirectory(Path.GetDirectoryName(plan)!);
        File.WriteAllText(plan, JsonSerializer.Serialize(new
        {
            install,
            version = "9.9.9",
            wait = new[] { 0 },
            hosts = new[] { new[] { "--hold", "--status", status } }
        }));
        try
        {
            var result = WinshellCli.Run([ReleaseUpdate.ApplyArgument, plan]);
            result.AssertExitCode(0);
            result.AssertNoError();
            UpdateStub.WaitUntil(() => Status(status, "NEW") is not null, null, "helper");
            var line = Status(status, "NEW")!;
            Assert.Equal("0", line[2]);
            Assert.Contains("--hold", line[3], StringComparison.Ordinal);
            Assert.Equal(
                File.ReadAllBytes(Path.Combine(UpdateStub.Build("NEW"), "winshellw.exe")),
                File.ReadAllBytes(Path.Combine(install, "bin", "winshellw.exe")));
            Assert.True(File.Exists(Path.Combine(install, "bin.old", "winshellw.exe")));
            var running = UpdateStub.Find(Path.Combine(install, "bin", "winshellw.exe"));
            Assert.NotNull(running);
            running!.Refresh();
            Assert.Equal(IntPtr.Zero, running.MainWindowHandle);
        }
        finally
        {
            UpdateStub.Release(install);
        }
    }

    private static string[]? Status(string path, string marker)
    {
        if (!File.Exists(path))
            return null;
        foreach (var raw in File.ReadAllLines(path))
        {
            var parts = raw.Split('\t');
            if (parts.Length == 4 && parts[0] == marker)
                return parts;
        }

        return null;
    }

    private sealed class ListedFeed : IReleaseFeed
    {
        private readonly ReleaseListing _listing;
        private readonly byte[] _zip;
        private readonly byte[] _sums;

        internal ListedFeed(ReleaseListing listing, byte[] zip, string name)
        {
            _listing = listing;
            _zip = zip;
            _sums = Encoding.UTF8.GetBytes(Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant() + "  " + name + "\n");
        }

        public ReleaseListing Latest(UpdateChannel channel) => _listing;

        public byte[] Download(string url) => url == _listing.ArchiveUrl ? _zip : _sums;
    }
}

internal static class UpdateStub
{
    private static readonly object Gate = new();

    private static readonly Dictionary<string, string> Built = new();

    internal static string Build(string marker)
    {
        lock (Gate)
        {
            if (Built.TryGetValue(marker, out var ready) && File.Exists(Path.Combine(ready, "winshellw.exe")))
                return ready;

            var root = Path.Combine(TempFolder.Root, "update-stub-" + marker);
            var project = Path.Combine(root, "src");
            var output = Path.Combine(root, "out");
            Directory.CreateDirectory(project);
            File.Copy(Repo("global.json"), Path.Combine(project, "global.json"), overwrite: true);
            File.WriteAllText(Path.Combine(project, "stub.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net11.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <AssemblyName>winshellw</AssemblyName>
                    <DebugType>none</DebugType>
                    <DebugSymbols>false</DebugSymbols>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(project, "Program.cs"), Program.Replace("__MARKER__", marker, StringComparison.Ordinal));
            var dotnet = Dotnet();
            var info = new ProcessStartInfo(dotnet)
            {
                WorkingDirectory = project,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            info.ArgumentList.Add("build");
            info.ArgumentList.Add("--nologo");
            info.ArgumentList.Add("-v");
            info.ArgumentList.Add("q");
            info.ArgumentList.Add("-o");
            info.ArgumentList.Add(output);
            var built = ChildProcess.Run(info, TimeSpan.FromMinutes(3));
            built.AssertExitCode(0);
            File.Copy(Path.Combine(output, "winshellw.exe"), Path.Combine(output, "winshell.exe"), overwrite: true);
            Built[marker] = output;
            return output;
        }
    }

    internal static byte[] Archive(string marker)
    {
        var directory = Build(marker);
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var name = Path.GetFileName(file);
                if (name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                    continue;
                var entry = archive.CreateEntry(name);
                using var body = entry.Open();
                body.Write(File.ReadAllBytes(file));
            }
        }

        return stream.ToArray();
    }

    internal static void Install(string marker, string bin)
    {
        Directory.CreateDirectory(bin);
        foreach (var file in Directory.EnumerateFiles(Build(marker)))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                continue;
            File.Copy(file, Path.Combine(bin, name), overwrite: true);
        }
    }

    internal static Process Start(string exe, string status, params string[] args)
    {
        var info = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        info.ArgumentList.Add("--status");
        info.ArgumentList.Add(status);
        return Process.Start(info)!;
    }

    internal static void WaitUntil(Func<bool> done, Process? process, string why)
    {
        var until = Environment.TickCount64 + 20_000;
        while (Environment.TickCount64 < until)
        {
            if (done())
                return;
            if (process is { HasExited: true })
                Assert.Fail(why + " exited " + process.ExitCode);
            Thread.Sleep(50);
        }

        Assert.Fail(why + " did not appear");
    }

    internal static Process? Find(string exe)
    {
        foreach (var process in Process.GetProcessesByName("winshellw"))
        {
            string? path = null;
            try
            {
                path = process.MainModule?.FileName;
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }

            if (path is not null && path.Equals(exe, StringComparison.OrdinalIgnoreCase))
                return process;
            process.Dispose();
        }

        return null;
    }

    internal static void Release(string root)
    {
        var exe = Path.Combine(root, "bin", "winshellw.exe");
        for (var attempt = 0; attempt < 40; attempt++)
        {
            KillUnder(root);
            if (!File.Exists(exe))
                return;
            try
            {
                using (File.Open(exe, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }

                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }
    }

    internal static void KillUnder(string root)
    {
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var process in Process.GetProcessesByName("winshellw"))
        {
            try
            {
                var path = process.MainModule?.FileName;
                if (path is not null && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5_000);
                }
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static string Dotnet()
    {
        var root = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(root))
        {
            var exe = Path.Combine(root, "dotnet.exe");
            if (File.Exists(exe))
                return exe;
        }

        return "dotnet";
    }

    private static string Repo(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, name);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(name + " was not found.");
    }

    private const string Program = """
        using System.Runtime.InteropServices;

        var marker = "__MARKER__";
        string? status = null;
        var hold = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--hold")
            {
                hold = true;
            }
            else if (args[i] == "--status" && i + 1 < args.Length)
            {
                status = args[++i];
            }
        }

        if (status is not null)
        {
            File.AppendAllText(status, marker + "\t" + (Environment.ProcessPath ?? "") + "\t" + Native.Window() + "\t" + string.Join("\u001f", args) + "\n");
        }

        if (hold)
        {
            Thread.Sleep(Timeout.Infinite);
        }

        static class Native
        {
            [DllImport("kernel32", EntryPoint = "GetConsoleWindow")]
            internal static extern nint Window();
        }
        """;
}
