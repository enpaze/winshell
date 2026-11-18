using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class CliJsonContext : JsonSerializerContext;

internal static class CliJson
{
    public static string Instances(IEnumerable<InstanceInfo> instances)
    {
        return Write(instances.Select(InstanceListItem.From).ToList(), CliJsonContext.Default.ListInstanceListItem);
    }

    public static string IpcShow(string payload)
    {
        var targets = IpcShowJson.Default.ListIpcTargetView;
        return Write(JsonSerializer.Deserialize(payload, targets) ?? [], targets);
    }

    private static string Write<T>(T value, JsonTypeInfo<T> type)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, JsonText.IndentedWriter))
        {
            JsonSerializer.Serialize(json, value, type);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
