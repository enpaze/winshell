using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Winshell;

internal sealed record InstanceInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("pid")] int Pid,
    [property: JsonPropertyName("shellId")]
    string ShellId,
    [property: JsonPropertyName("configPath")]
    string ConfigPath,
    [property: JsonPropertyName("launchTime")]
    DateTime LaunchTime,
    [property: JsonPropertyName("pipe")] string Pipe,
    [property: JsonPropertyName("log")] string Log,
    [property: JsonPropertyName("processStart")]
    long ProcessStart = 0,
    [property: JsonPropertyName("exitedAt")]
    DateTime? ExitedAt = null,
    [property: JsonPropertyName("processName")]
    string? ProcessName = null);

[JsonSerializable(typeof(InstanceInfo))]
[JsonSerializable(typeof(List<InstanceInfo>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class InstanceJson : JsonSerializerContext;

internal sealed class Instances(string folder)
{
    private const string RecordName = "instance.json";

    public static string NewId()
    {
        return Convert.ToHexStringLower(Guid.NewGuid().ToByteArray())[..12];
    }

    public string DirOf(string id)
    {
        return Path.Combine(folder, id);
    }

    public void Write(InstanceInfo info)
    {
        var dir = DirOf(info.Id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, RecordName),
            JsonSerializer.Serialize(info, InstanceJson.Default.InstanceInfo));
    }

    public void Remove(string id)
    {
        if (Read(DirOf(id)) is not { ExitedAt: null } info)
            return;
        try
        {
            Write(info with { ExitedAt = TimeProvider.System.GetLocalNow().LocalDateTime });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    public List<(InstanceInfo Info, bool Alive)> List(bool includeDead)
    {
        var result = new List<(InstanceInfo Info, bool Alive)>();
        if (!Directory.Exists(folder))
            return result;
        foreach (var dir in Directory.EnumerateDirectories(folder))
        {
            if (Read(dir) is not { } info)
                continue;
            var alive = IsRunning(info);
            if (alive || includeDead)
                result.Add((info, alive));
        }

        return result.Where(row => !row.Alive)
            .Concat(result.Where(row => row.Alive).GroupBy(row => row.Info.Pid)
                .Select(group => group.MaxBy(row => row.Info.LaunchTime)))
            .OrderByDescending(row => row.Info.LaunchTime).ToList();
    }

    private static InstanceInfo? Read(string dir)
    {
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(dir, RecordName)),
                InstanceJson.Default.InstanceInfo);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    internal static bool ProcessAlive(int pid)
    {
        if (pid <= 0)
            return false;
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    internal static bool IsRunning(InstanceInfo info)
    {
        if (info.ExitedAt is not null)
            return false;
        try
        {
            using var process = Process.GetProcessById(info.Pid);
            return Wrote(info, process);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    internal static bool Wrote(InstanceInfo info, Process process)
    {
        try
        {
            if (!string.Equals(process.ProcessName, info.ProcessName, StringComparison.OrdinalIgnoreCase) ||
                process.HasExited)
                return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return true;
        }

        try
        {
            return process.StartTime.ToUniversalTime().Ticks == info.ProcessStart;
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception or ArgumentException)
        {
            return true;
        }
    }

    internal static void WaitForExit(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.WaitForExit(TimeSpan.FromSeconds(30));
        }
        catch (ArgumentException)
        {
        }
    }

    public void Clean()
    {
        if (!Directory.Exists(folder))
            return;
        foreach (var dir in Directory.EnumerateDirectories(folder))
        {
            var info = Read(dir);
            if (info is null ? File.Exists(Path.Combine(dir, RecordName)) : IsRunning(info))
                continue;
            if (TimeProvider.System.GetLocalNow().LocalDateTime - LastUsed(dir, info) > TimeSpan.FromDays(1))
                TryDelete(dir);
        }
    }

    private static DateTime LastUsed(string dir, InstanceInfo? info)
    {
        if (info?.ExitedAt is { } exited)
            return exited;
        var log = Path.Combine(dir, InstanceLog.FileName);
        if (File.Exists(log))
            return File.GetLastWriteTime(log);
        return info?.LaunchTime ?? Directory.GetLastWriteTime(dir);
    }

    private static void TryDelete(string dir)
    {
        try
        {
            Directory.Delete(dir, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
