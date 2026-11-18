using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Management;
using System.Text;
using System.Text.Json;
using Winshell.Native;
using WindowsCommand = Winshell.Native.CommandLine;

namespace Winshell;

internal static class ReleaseUpdate
{
    internal const string ApplyArgument = "--apply-staged-update";

    internal static async Task<UpdateResult> ApplyAsync(
        IReleaseFeed feed,
        UpdateChannel channel,
        string installRoot,
        string executablePath,
        string informationalVersion,
        Func<Task<bool>>? stopShells = null,
        Action? beforeWait = null)
    {
        installRoot = Path.GetFullPath(installRoot);
        if (!Installed(executablePath, installRoot))
            return new UpdateResult.NotInstalled();

        ClearIdle(installRoot);
        var listing = feed.Latest(channel);
        if (ReleaseSources.IsCurrent(channel, informationalVersion, listing))
            return new UpdateResult.Current(listing.Version);

        var archive = ReleaseSources.ZipName(listing.Version);
        var zip = feed.Download(listing.ArchiveUrl);
        var sums = Encoding.UTF8.GetString(feed.Download(listing.ChecksumsUrl));
        if (!ReleaseSources.ChecksumMatches(sums, archive, zip))
            return new UpdateResult.Rejected(archive);

        var stage = Path.Combine(installRoot, "bin.new");
        Extract(zip, stage);
        var hosts = Record(installRoot);
        if (stopShells is not null && !await stopShells())
        {
            DeleteDirectory(stage);
            throw new UpdateFailedException();
        }

        var wait = hosts.Select(host => host.Pid).ToArray();
        var arguments = hosts.Select(host => host.Arguments).ToArray();
        if (RunningFrom(installRoot))
            return HandOff(installRoot, listing.Version, wait, arguments);

        return Finish(installRoot, listing.Version, wait, arguments, beforeWait);
    }

    internal static void FinishPlan(string planPath)
    {
        var full = Path.GetFullPath(planPath);
        using var document = JsonDocument.Parse(File.ReadAllText(full));
        var root = document.RootElement;
        var install = Path.GetFullPath(Required(root, "install"));
        var version = Required(root, "version");
        var swap = Path.Combine(install, "swap");
        if (!Under(full, swap))
            throw new InvalidOperationException("The staged update plan is not in the install.");

        var self = Environment.ProcessPath;
        if (self is not null && Under(self, install) && !Under(self, swap))
            throw new InvalidOperationException("The staged update is not running from the staged copy.");

        Finish(install, version, Numbers(root, "wait"), Arguments(root), null);
    }

    private static UpdateResult HandOff(string installRoot, string version, int[] wait, string[][] hosts)
    {
        var stage = Path.Combine(installRoot, "bin.new");
        var swap = Path.Combine(installRoot, "swap");
        CopyTree(stage, swap);
        var plan = Path.Combine(swap, "plan.json");
        WritePlan(plan, installRoot, version, [.. wait, Environment.ProcessId], hosts);
        var error = InstanceStart.Detach(Path.Combine(swap, "winshellw.exe"), new[] { ApplyArgument, plan });
        if (error is not null)
        {
            DeleteDirectory(swap);
            DeleteDirectory(stage);
            throw new InvalidOperationException(error);
        }

        return new UpdateResult.Updated(version, hosts.Length > 0);
    }

    private static UpdateResult Finish(string installRoot, string version, int[] wait, string[][] hosts, Action? beforeWait)
    {
        beforeWait?.Invoke();
        foreach (var pid in wait.Distinct())
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                process.WaitForExit();
            }
            catch (Exception e) when (e is ArgumentException or Win32Exception or InvalidOperationException)
            {
            }
        }

        var bin = Path.Combine(installRoot, "bin");
        var stage = Path.Combine(installRoot, "bin.new");
        var old = Path.Combine(installRoot, "bin.old");
        if (Directory.Exists(old))
        {
            if (Busy(old))
                throw new InvalidOperationException("Winshell is still running from its previous install.");
            DeleteDirectory(old);
        }

        Directory.Move(bin, old);
        Directory.Move(stage, bin);
        var program = Path.Combine(bin, "winshellw.exe");
        var started = hosts.Length > 0 ? hosts : new[] { new[] { "--no-duplicate" } };
        foreach (var arguments in started)
        {
            var error = InstanceStart.Detach(program, arguments);
            if (error is not null)
                throw new InvalidOperationException(error);
        }

        return new UpdateResult.Updated(version, hosts.Length > 0);
    }

    private static void ClearIdle(string installRoot)
    {
        var swap = Path.Combine(installRoot, "swap");
        if (Directory.Exists(swap))
        {
            if (Busy(swap))
                throw new InvalidOperationException("An update is already replacing Winshell.");
            DeleteDirectory(swap);
        }

        var old = Path.Combine(installRoot, "bin.old");
        if (Directory.Exists(old))
        {
            if (Busy(old))
                throw new InvalidOperationException("Winshell is still running from its previous install.");
            DeleteDirectory(old);
        }

        DeleteDirectory(Path.Combine(installRoot, "bin.new"));
    }

    private static void Extract(byte[] bytes, string stage)
    {
        Directory.CreateDirectory(stage);
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            try
            {
                ZipFile.ExtractToDirectory(stream, stage, overwriteFiles: true);
            }
            catch (IOException)
            {
                throw new InvalidOperationException("The release archive is not a winshell folder.");
            }

            if (!File.Exists(Path.Combine(stage, "winshell.exe")) || !File.Exists(Path.Combine(stage, "winshellw.exe")))
                throw new InvalidOperationException("The release archive has no winshell.exe and winshellw.exe.");
        }
        catch
        {
            DeleteDirectory(stage);
            throw;
        }
    }

    private static List<Host> Record(string installRoot)
    {
        var lines = CommandLines();
        var hosts = new List<Host>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == 0 || process.Id == Environment.ProcessId)
                    continue;
                var image = Executable(process.Id);
                if (image is null || !Under(image, installRoot))
                    continue;
                lines.TryGetValue(process.Id, out var command);
                var split = WindowsCommand.Split(command ?? "");
                hosts.Add(new Host(process.Id, split.Length == 0 ? [] : split[1..]));
            }
        }

        return hosts;
    }

    private static Dictionary<int, string> CommandLines()
    {
        var lines = new Dictionary<int, string>();
        using var search = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process");
        using var found = search.Get();
        foreach (ManagementObject row in found)
        {
            using (row)
            {
                var pid = row["ProcessId"] switch
                {
                    uint number => unchecked((int)number),
                    int number => number,
                    _ => 0
                };
                lines[pid] = row["CommandLine"] as string ?? "";
            }
        }

        return lines;
    }

    private static bool Busy(string directory)
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == 0 || process.Id == Environment.ProcessId)
                    continue;
                var image = Executable(process.Id);
                if (image is not null && Under(image, directory))
                    return true;
            }
        }

        return false;
    }

    private static string? Executable(int pid)
    {
        try
        {
            return ProcessImage.TryRead((uint)pid);
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool Installed(string executablePath, string installRoot)
    {
        return !string.IsNullOrWhiteSpace(executablePath) && Under(executablePath, Path.Combine(installRoot, "bin"));
    }

    private static bool RunningFrom(string installRoot)
    {
        var path = Environment.ProcessPath;
        return path is not null
               && (Under(path, Path.Combine(installRoot, "bin"))
                   || Under(path, Path.Combine(installRoot, "bin.new"))
                   || Under(path, Path.Combine(installRoot, "bin.old")));
    }

    private static bool Under(string path, string root)
    {
        var full = Path.GetFullPath(path);
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyTree(string source, string destination)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private static void WritePlan(string path, string install, string version, int[] wait, string[][] hosts)
    {
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteString("install", install);
        writer.WriteString("version", version);
        writer.WriteStartArray("wait");
        foreach (var pid in wait)
            writer.WriteNumberValue(pid);
        writer.WriteEndArray();
        writer.WritePropertyName("hosts");
        writer.WriteStartArray();
        foreach (var host in hosts)
        {
            writer.WriteStartArray();
            foreach (var argument in host)
                writer.WriteStringValue(argument);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static string Required(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || value.GetString() is not { Length: > 0 } text)
            throw new InvalidOperationException("The staged update plan could not be read.");

        return text;
    }

    private static int[] Numbers(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("The staged update plan could not be read.");

        var numbers = new List<int>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var pid))
                throw new InvalidOperationException("The staged update plan could not be read.");
            numbers.Add(pid);
        }

        return numbers.ToArray();
    }

    private static string[][] Arguments(JsonElement root)
    {
        if (!root.TryGetProperty("hosts", out var value) || value.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("The staged update plan could not be read.");

        var hosts = new List<string[]>();
        foreach (var host in value.EnumerateArray())
        {
            if (host.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("The staged update plan could not be read.");
            var arguments = new List<string>();
            foreach (var argument in host.EnumerateArray())
            {
                if (argument.ValueKind != JsonValueKind.String || argument.GetString() is not { } text)
                    throw new InvalidOperationException("The staged update plan could not be read.");
                arguments.Add(text);
            }

            hosts.Add(arguments.ToArray());
        }

        return hosts.ToArray();
    }

    private sealed record Host(int Pid, string[] Arguments);
}
