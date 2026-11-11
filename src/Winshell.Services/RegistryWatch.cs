using Microsoft.Win32;
using Winshell.Native;

namespace Winshell;

/// <summary>
/// Watches a set of <c>HKCU</c> keys. Windows signals the request once, so a signal arms every key again and then
/// calls back. A key that cannot be opened is skipped. When none can be armed, the callback is never called. The
/// callback runs on a thread-pool thread, including once more after <see cref="Dispose"/> has started.
/// </summary>
internal sealed class RegistryWatch : IDisposable
{
    private readonly Action _changed;
    private readonly List<RegistryKey> _keys = [];
    private readonly AutoResetEvent _event = new(false);
    private readonly Lock _gate = new();
    private RegisteredWaitHandle? _wait;
    private bool _disposed;

    /// <summary>Opens <paramref name="keyPaths"/> under <c>HKCU</c> and arms the watch.</summary>
    /// <param name="keyPaths">Key paths. A path that cannot be opened is skipped.</param>
    /// <param name="changed">Called after each signal, after the watch is armed again.</param>
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
