using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Winshell;

internal sealed record InstanceListItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("pid")] int Pid,
    [property: JsonPropertyName("shellId")]
    string ShellId,
    [property: JsonPropertyName("configPath")]
    string ConfigPath,
    [property: JsonPropertyName("launchTime")]
    DateTime LaunchTime,
    [property: JsonPropertyName("pipe")] string Pipe,
    [property: JsonPropertyName("log")] string Log)
{
    public static InstanceListItem From(InstanceInfo info)
    {
        return new InstanceListItem(info.Id, info.Pid, info.ShellId, info.ConfigPath, info.LaunchTime, info.Pipe,
            info.Log);
    }
}

[JsonSerializable(typeof(List<InstanceListItem>))]
[JsonSerializable(typeof(List<IpcTargetView>))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class CliJsonContext : JsonSerializerContext;

internal static class CliJson
{
    public static string Instances(IEnumerable<InstanceInfo> instances)
    {
        return Write(instances.Select(InstanceListItem.From).ToList());
    }

    public static string IpcShow(IReadOnlyList<IpcTargetView> targets)
    {
        return Write(targets as List<IpcTargetView> ?? targets.ToList());
    }

    public static string IpcShow(string payload)
    {
        var targets = JsonSerializer.Deserialize(payload, CliJsonContext.Default.ListIpcTargetView) ?? [];
        return IpcShow(targets);
    }

    private static string Write<T>(T value)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, JsonText.IndentedWriter))
        {
            JsonSerializer.Serialize(json, value, CliJsonContext.Default.Options);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
