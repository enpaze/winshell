using Microsoft.Win32;
using Winshell.Apps;
using Winshell.Native;

namespace Winshell.Tray;

internal sealed record TrayIconSetting(Guid Guid, string ExecutablePath, uint? Id, bool Promoted);

internal static class TrayIconSettings
{
    private const string KeyPath = @"Control Panel\NotifyIconSettings";

    public static List<TrayIconSetting> Read()
    {
        using var root = Registry.CurrentUser.OpenSubKey(KeyPath);
        return root is null ? [] : Read(root);
    }

    public static List<TrayIconSetting> Read(RegistryKey root)
    {
        var settings = new List<TrayIconSetting>();
        foreach (var name in root.GetSubKeyNames())
        {
            using var key = root.OpenSubKey(name);
            if (key is null)
                continue;
            var guid = key.GetValue("IconGuid") is string text && Guid.TryParse(text, out var g) ? g : Guid.Empty;
            var path = key.GetValue("ExecutablePath") is string p ? AppIdentity.ExpandKnownFolder(p) : "";
            uint? id = key.GetValue("UID") is int uid ? unchecked((uint)uid) : null;
            var promoted = key.GetValue("IsPromoted") is int and not 0;
            settings.Add(new TrayIconSetting(guid, path, id, promoted));
        }

        return settings;
    }

    public static bool InOverflow(IEnumerable<TrayIconSetting> settings, Guid guid, string program, uint id)
    {
        foreach (var s in settings)
        {
            var match = guid != Guid.Empty
                ? s.Guid == guid
                : s.Guid == Guid.Empty && s.Id == id && program.Length > 0 &&
                  string.Equals(s.ExecutablePath, program, StringComparison.OrdinalIgnoreCase);
            if (match)
                return !s.Promoted;
        }

        return true;
    }

    public static IDisposable Watch(IEventLoop loop, Action changed)
    {
        return new Watcher(loop, changed);
    }

    private sealed class Watcher : IDisposable
    {
        private readonly IEventLoop _loop;
        private readonly Action _changed;
        private readonly RegistryKey? _key;
        private readonly AutoResetEvent _event = new(false);
        private readonly RegisteredWaitHandle? _wait;
        private IDisposable? _settle;
        private bool _disposed;

        public Watcher(IEventLoop loop, Action changed)
        {
            _loop = loop;
            _changed = changed;
            _key = Registry.CurrentUser.OpenSubKey(KeyPath);
            if (_key is null || !Arm())
                return;
            _wait = ThreadPool.RegisterWaitForSingleObject(_event, (_, _) => _loop.Post(OnChanged), null,
                Timeout.Infinite, false);
        }

        private bool Arm()
        {
            return RegistryChange.Notify(_key!.Handle, _event.SafeWaitHandle);
        }

        private void OnChanged()
        {
            if (_disposed)
                return;
            Arm();
            _settle?.Dispose();
            _settle = _loop.Schedule(TimeSpan.FromMilliseconds(100), () =>
            {
                _settle = null;
                if (!_disposed)
                    _changed();
            });
        }

        public void Dispose()
        {
            _disposed = true;
            _settle?.Dispose();
            _wait?.Unregister(null);
            _key?.Dispose();
            _event.Dispose();
        }
    }
}
