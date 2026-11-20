using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Winshell.Winland;

internal static class WinlandProtocol
{
    public const int ReplyLimit = 1024 * 1024;

    public static IReadOnlyList<string> PipeNames(string? overrideName, int sessionId)
    {
        return string.IsNullOrWhiteSpace(overrideName)
            ? ["winland-" + sessionId.ToString(CultureInfo.InvariantCulture), "winland"]
            : [overrideName.Trim()];
    }

    public const string EventsRequest = "j/events";

    public const string MonitorsRequest = "j/get monitors";

    public const string WorkspacesRequest = "j/get workspaces";

    public const string WindowsRequest = "j/get windows";

    public const string ActiveWindowRequest = "j/get window";

    public const string FilesRequest = "j/get files";

    public static WinlandEvent? ParseEvent(string line)
    {
        return ParseJson(line) is { ValueKind: JsonValueKind.Object } json && Str(json, "event") is { Length: > 0 } name
            ? new WinlandEvent(name, json)
            : null;
    }

    public static Event? ReadEvent(WinlandEvent winlandEvent)
    {
        var data = winlandEvent.Data;
        switch (winlandEvent.Name)
        {
            case "configreloaded":
                return new ConfigReloaded();
            case "monitoradded":
                return new MonitorAdded(Int(data, "id") ?? -1, Str(data, "name"), Str(data, "description"));
            case "monitorremoved":
                return new MonitorRemoved(Str(data, "name"));
            case "createworkspace":
                {
                    var (id, workspace) = Workspace(data);
                    return new WorkspaceCreated(id, workspace, Str(data, "monitor"));
                }
            case "destroyworkspace":
                {
                    var (id, workspace) = Workspace(data);
                    return new WorkspaceDestroyed(id, workspace, Str(data, "monitor"));
                }
            case "focus":
                {
                    var (id, workspace) = Workspace(data);
                    return new Focused(Str(data, "monitor"), workspace.Length > 0 ? workspace : null, id,
                        ParseAddress(Str(Obj(data, "window"), "address")));
                }
            case "workspace":
                {
                    var (id, workspace) = Workspace(data);
                    return new WorkspaceShown(id, workspace, Str(data, "monitor"));
                }
            case "moveworkspace":
                {
                    var (id, workspace) = Workspace(data);
                    return new WorkspaceMoved(id, workspace, Str(data, "monitor"));
                }
            case "fullscreen":
                return new FullscreenChanged(Bool(data, "fullscreen"));
            case "openwindow":
                return new WindowOpened(ParseAddress(Str(data, "address")), Workspace(data).Name, Str(data, "monitor"),
                    Str(data, "class"), Str(data, "title"));
            case "closewindow":
                return new WindowClosed(ParseAddress(Str(data, "address")));
            case "movewindow":
                {
                    var (id, workspace) = Workspace(data);
                    return new WindowMoved(ParseAddress(Str(data, "address")), id, workspace, Str(data, "monitor"));
                }
            case "windowtitle":
                return new WindowTitleChanged(ParseAddress(Str(data, "address")), Str(data, "title"));
            case "urgent":
                return new WindowUrgent(ParseAddress(Str(data, "address")));
            default:
                return null;
        }
    }

    public static JsonElement? ParseJson(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(reply);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static (int Id, string Name) Workspace(JsonElement data)
    {
        var workspace = Obj(data, "workspace");
        var name = Str(workspace, "name");
        return (Int(workspace, "id") ?? IdFromName(name), name);
    }

    public static ulong ParseAddress(string text)
    {
        var s = text.AsSpan().Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            s = s[2..];
        return ulong.TryParse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private const string DefaultSpecial = "special:special";

    private const int DefaultSpecialId = -99;
    private const int FirstNamedId = -1;

    public static int? Integer(string text)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    public static Dictionary<string, int> UnnumberedIds(IEnumerable<string> names)
    {
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        var distinct = names.Distinct(StringComparer.Ordinal).Where(n => Integer(n) is null).ToList();
        var named = distinct.Where(n => !IsSpecial(n)).Order(StringComparer.Ordinal).ToList();
        for (var i = 0; i < named.Count; i++)
            ids[named[i]] = FirstNamedId - i;
        var next = DefaultSpecialId + 1;
        foreach (var special in distinct.Where(IsSpecial).Order(StringComparer.Ordinal))
            ids[special] = special == DefaultSpecial ? DefaultSpecialId : next++;
        return ids;
    }

    public static bool IsSpecial(string name)
    {
        return name.StartsWith("special:", StringComparison.Ordinal);
    }

    public static int IdFromName(string name)
    {
        return Integer(name) ?? (name == DefaultSpecial ? DefaultSpecialId : FirstNamedId);
    }

    public static string Str(JsonElement obj, string name)
    {
        return obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) &&
               v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? ""
            : "";
    }

    public static int? Int(JsonElement obj, string name)
    {
        return obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) &&
               v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)
            ? i
            : null;
    }

    public static double Num(JsonElement obj, string name)
    {
        return obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) &&
               v.ValueKind == JsonValueKind.Number
            ? v.GetDouble()
            : 0;
    }

    public static bool Bool(JsonElement obj, string name)
    {
        return obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) &&
               v.ValueKind == JsonValueKind.True;
    }

    public static JsonElement Obj(JsonElement obj, string name)
    {
        return obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) ? v : default;
    }

    internal abstract record Event;

    internal sealed record ConfigReloaded : Event;

    internal sealed record MonitorAdded(int Id, string Name, string Description) : Event;

    internal sealed record MonitorRemoved(string Name) : Event;

    internal sealed record WorkspaceCreated(int Id, string Name, string Monitor) : Event;

    internal sealed record WorkspaceDestroyed(int Id, string Name, string Monitor) : Event;

    internal sealed record Focused(string Monitor, string? Workspace, int WorkspaceId, ulong Window) : Event;

    internal sealed record WorkspaceShown(int Id, string Name, string Monitor) : Event;

    internal sealed record WorkspaceMoved(int Id, string Name, string Monitor) : Event;

    internal sealed record FullscreenChanged(bool Fullscreen) : Event;

    internal sealed record WindowOpened(ulong Address, string Workspace, string Monitor, string WindowClass, string Title) : Event;

    internal sealed record WindowClosed(ulong Address) : Event;

    internal sealed record WindowMoved(ulong Address, int WorkspaceId, string Workspace, string Monitor) : Event;

    internal sealed record WindowTitleChanged(ulong Address, string Title) : Event;

    internal sealed record WindowUrgent(ulong Address) : Event;
}

internal sealed class WinlandReader(Stream stream)
{
    private readonly byte[] _buffer = new byte[8192];
    private int _start;
    private int _end;

    public async Task<string?> ReadLineAsync(CancellationToken cancel = default)
    {
        using var line = new MemoryStream();
        while (true)
        {
            if (_start == _end && !await FillAsync(cancel).ConfigureAwait(false))
                return line.Length == 0 ? null : Decode(line);
            var newline = Array.IndexOf(_buffer, (byte)'\n', _start, _end - _start);
            var stop = newline < 0 ? _end : newline;
            line.Write(_buffer, _start, stop - _start);
            if (line.Length > WinlandProtocol.ReplyLimit)
                throw new InvalidDataException("The line is too long.");
            _start = newline < 0 ? _end : newline + 1;
            if (newline >= 0)
                return Decode(line);
        }
    }

    public async Task<string> ReadReplyAsync(CancellationToken cancel = default)
    {
        if (await ReadLineAsync(cancel).ConfigureAwait(false) is not { } first)
            return "";
        if (first.Length <= 1 || first[0] != '#' || !int.TryParse(first.AsSpan(1), NumberStyles.None,
                CultureInfo.InvariantCulture, out var length))
            return first;
        if (length > WinlandProtocol.ReplyLimit)
            throw new InvalidDataException("The reply is too large.");
        var body = new byte[length];
        var got = 0;
        while (got < length)
        {
            if (_start == _end && !await FillAsync(cancel).ConfigureAwait(false))
                throw new EndOfStreamException("The reply was cut short.");
            var take = Math.Min(length - got, _end - _start);
            Array.Copy(_buffer, _start, body, got, take);
            _start += take;
            got += take;
        }

        return Encoding.UTF8.GetString(body);
    }

    private async Task<bool> FillAsync(CancellationToken cancel)
    {
        _start = 0;
        _end = await stream.ReadAsync(_buffer, cancel).ConfigureAwait(false);
        return _end > 0;
    }

    private static string Decode(MemoryStream line)
    {
        var bytes = line.GetBuffer().AsSpan(0, (int)line.Length);
        if (bytes.Length > 0 && bytes[^1] == '\r')
            bytes = bytes[..^1];
        return Encoding.UTF8.GetString(bytes);
    }
}
