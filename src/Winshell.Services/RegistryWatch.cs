using Microsoft.Win32;
using Winshell.Native;

namespace Winshell;

internal sealed class RegistryWatch : IDisposable
{
    private readonly Action _changed;
    private readonly List<RegistryKey> _keys = [];
    private readonly AutoResetEvent _event = new(false);
    private readonly Lock _gate = new();
    private RegisteredWaitHandle? _wait;
    private bool _disposed;

    internal RegistryWatch(IReadOnlyList<string> keyPaths, Action changed)
    {
        _changed = changed;
        foreach (var path in keyPaths)
        {
            var key = Registry.CurrentUser.OpenSubKey(path);
            if (key is not null)
                _keys.Add(key);
        }

        if (_keys.Count == 0 || !Arm())
            return;
        _wait = ThreadPool.RegisterWaitForSingleObject(_event, (_, _) => OnSignaled(), null, Timeout.Infinite, false);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        _wait?.Unregister(null);
        _wait = null;
        foreach (var key in _keys)
            key.Dispose();
        _keys.Clear();
        _event.Dispose();
    }

    private bool Arm()
    {
        var armed = false;
        foreach (var key in _keys)
            armed |= RegistryChange.Notify(key.Handle, _event.SafeWaitHandle);
        return armed;
    }

    private void OnSignaled()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            Arm();
        }

        _changed();
    }
}
