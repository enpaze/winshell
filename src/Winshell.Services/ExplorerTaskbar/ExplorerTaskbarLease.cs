using System.Text.Json;
using System.Text.Json.Serialization;
using Winshell.Native;

namespace Winshell.ExplorerTaskbar;

internal static partial class ExplorerTaskbarLease
{
    private const string FileName = "explorer-taskbar.json";

    internal static bool Exists(ShellLocations locations)
    {
        return File.Exists(PathFor(locations));
    }

    internal static bool LeaveHidden(int childPid, bool hiddenAtStart)
    {
        return LeaveHidden(ShellLocations.Current, childPid, hiddenAtStart);
    }

    internal static bool LeaveHidden(ShellLocations locations, int childPid, bool hiddenAtStart)
    {
        if (!TryRead(locations, out var lease))
            return hiddenAtStart;
        if (lease.Owner == 0)
            return true;
        if (lease.Owner == childPid || !Instances.ProcessAlive(lease.Owner))
            return false;
        return true;
    }

    internal static void Recover()
    {
        var locations = ShellLocations.Current;
        if (!TryRead(locations, out var lease))
        {
            ExplorerTaskbarRecovery.RestoreHidden();
            return;
        }

        Restore(locations, lease);
    }

    internal static void ReleaseUnclaimed()
    {
        ReleaseUnclaimed(ShellLocations.Current);
    }

    internal static void ReleaseUnclaimed(ShellLocations locations)
    {
        if (!TryRead(locations, out var lease))
            return;
        if (lease.Owner != 0 && lease.Owner != Environment.ProcessId && Instances.ProcessAlive(lease.Owner))
            return;
        Restore(locations, lease);
    }

    internal static List<ExplorerTaskbarWindow.Saved> Windows(ShellLocations locations)
    {
        return TryRead(locations, out var lease) ? lease.Windows.Select(ToSaved).ToList() : [];
    }

    internal static bool Write(ShellLocations locations, int owner, IReadOnlyList<ExplorerTaskbarWindow.Saved> windows)
    {
        var file = new TaskbarLeaseFile(owner, windows.Select(FromSaved).ToList());
        return Save(locations, file);
    }

    internal static void HandOff(ShellLocations locations)
    {
        if (!TryRead(locations, out var lease) || lease.Owner == 0)
            return;
        Save(locations, lease with { Owner = 0 });
    }

    internal static void HandOff()
    {
        HandOff(ShellLocations.Current);
    }

    internal static void Forget(ShellLocations locations)
    {
        try
        {
            File.Delete(PathFor(locations));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void Restore(ShellLocations locations, TaskbarLeaseFile lease)
    {
        foreach (var window in lease.Windows)
            ExplorerTaskbarWindow.PutBack(ToSaved(window));
        Forget(locations);
    }

    private static bool Save(ShellLocations locations, TaskbarLeaseFile lease)
    {
        var path = PathFor(locations);
        var temp = path + ".tmp";
        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } folder)
                Directory.CreateDirectory(folder);
            File.WriteAllText(temp, JsonSerializer.Serialize(lease, TaskbarLeaseJson.Default.TaskbarLeaseFile));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception delete) when (delete is IOException or UnauthorizedAccessException)
            {
            }

            return false;
        }
    }

    private static bool TryRead(ShellLocations locations, out TaskbarLeaseFile lease)
    {
        lease = null!;
        try
        {
            var path = PathFor(locations);
            if (!File.Exists(path))
                return false;
            lease = JsonSerializer.Deserialize(File.ReadAllText(path), TaskbarLeaseJson.Default.TaskbarLeaseFile)
                    ?? throw new JsonException("The taskbar lease is empty.");
            lease = lease with { Windows = lease.Windows ?? [] };
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static string PathFor(ShellLocations locations)
    {
        return Path.Combine(locations.LocalRoot, FileName);
    }

    private static TaskbarWindowState FromSaved(ExplorerTaskbarWindow.Saved saved)
    {
        return new TaskbarWindowState(saved.Hwnd, saved.WasVisible, saved.Reserves, saved.Removed, (int)saved.Edge,
            saved.Rect.left, saved.Rect.top, saved.Rect.right, saved.Rect.bottom);
    }

    private static ExplorerTaskbarWindow.Saved ToSaved(TaskbarWindowState state)
    {
        return new ExplorerTaskbarWindow.Saved
        {
            Hwnd = (nint)state.Hwnd,
            WasVisible = state.WasVisible,
            Reserves = state.Reserves,
            Removed = state.Removed,
            Edge = (AppBarEdge)state.Edge,
            Rect = new RECT
            {
                left = state.Left,
                top = state.Top,
                right = state.Right,
                bottom = state.Bottom
            }
        };
    }

    private sealed record TaskbarLeaseFile(
        [property: JsonPropertyName("owner")] int Owner,
        [property: JsonPropertyName("windows")] List<TaskbarWindowState> Windows);

    private sealed record TaskbarWindowState(
        [property: JsonPropertyName("hwnd")] long Hwnd,
        [property: JsonPropertyName("wasVisible")] bool WasVisible,
        [property: JsonPropertyName("reserves")] bool Reserves,
        [property: JsonPropertyName("removed")] bool Removed,
        [property: JsonPropertyName("edge")] int Edge,
        [property: JsonPropertyName("left")] int Left,
        [property: JsonPropertyName("top")] int Top,
        [property: JsonPropertyName("right")] int Right,
        [property: JsonPropertyName("bottom")] int Bottom);

    [JsonSerializable(typeof(TaskbarLeaseFile))]
    private sealed partial class TaskbarLeaseJson : JsonSerializerContext;
}
