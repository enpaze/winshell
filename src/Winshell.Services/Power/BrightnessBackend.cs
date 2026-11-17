using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using Winshell.Native;

namespace Winshell.Power;

public enum BrightnessConnection
{
    Internal = 0,

    External = 1
}

internal sealed record BrightnessInfo(
    string DeviceName,
    string Name,
    BrightnessConnection Connection,
    int Minimum,
    int Maximum,
    int Current);

internal sealed record BrightnessSnapshot(bool Ready, IReadOnlyList<BrightnessInfo> Monitors);

internal interface IBrightnessBackend : IDisposable
{
    event Action? Changed;

    Task<BrightnessSnapshot> ReadAsync(CancellationToken cancellation);

    string? Set(string deviceName, int brightness);
}

internal sealed class BrightnessBackend(Log log) : IBrightnessBackend
{
    private volatile IReadOnlyList<BrightnessInfo> _monitors = [];
    private bool _watching;
    private ManagementEventWatcher? _watcher;

    public event Action? Changed;

    public Task<BrightnessSnapshot> ReadAsync(CancellationToken cancellation)
    {
        return Task.Run(Read, cancellation);
    }

    private BrightnessSnapshot Read()
    {
        if (!_watching)
        {
            _watching = true;
            WatchInternal();
        }

        _monitors = Combine(Screens.Enumerate(), ReadWmi(), ReadExternal, _monitors);
        return new BrightnessSnapshot(true, _monitors);
    }

    public void Dispose()
    {
        if (!_watching)
            return;
        _watching = false;
        if (_watcher is { } watcher)
        {
            watcher.EventArrived -= OnPanelChanged;
            watcher.Stop();
            watcher.Dispose();
            _watcher = null;
        }
    }

    private void OnPanelChanged(object sender, EventArrivedEventArgs e)
    {
        Changed?.Invoke();
    }

    private void WatchInternal()
    {
        try
        {
            var watcher = new ManagementEventWatcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessEvent");
            watcher.EventArrived += OnPanelChanged;
            watcher.Start();
            _watcher = watcher;
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException
                                       or PlatformNotSupportedException)
        {
            WmiFailed("watch built-in panel brightness", ex);
        }
    }

    internal static List<BrightnessInfo> Combine(IReadOnlyList<ConnectedMonitor> screens,
        IReadOnlyList<WmiPanel> panels, Func<nint, (int Minimum, int Current, int Maximum)?> readExternal,
        IReadOnlyList<BrightnessInfo> previous)
    {
        var unmatched = panels.ToList();
        var result = new List<BrightnessInfo>();
        foreach (var screen in screens)
        {
            var panel = unmatched.FirstOrDefault(p =>
                screen.ModelCode.Length > 0
                && string.Equals(Screens.ModelCode(p.Instance), screen.ModelCode, StringComparison.OrdinalIgnoreCase));
            if (panel is not null)
            {
                unmatched.Remove(panel);
                result.Add(Internal(panel, screen.Info.Model.Length > 0 ? screen.Info.Model : panel.Instance));
                continue;
            }

            var external = External(screen.Info.Id, screen.Info.Model.Length > 0 ? screen.Info.Model : screen.Info.Name,
                readExternal(screen.Handle), previous);
            if (external is not null)
                result.Add(external);
        }

        result.AddRange(unmatched.Select(p => Internal(p, p.Instance)));
        return result;
    }

    private static BrightnessInfo Internal(WmiPanel panel, string name)
    {
        return new BrightnessInfo(panel.Instance, name, BrightnessConnection.Internal, panel.Minimum, panel.Maximum,
            panel.Current);
    }

    internal static BrightnessInfo? External(string id, string name, (int Minimum, int Current, int Maximum)? reading,
        IReadOnlyList<BrightnessInfo> previous)
    {
        return reading is { } r
            ? new BrightnessInfo(id, name, BrightnessConnection.External, r.Minimum, r.Maximum, r.Current)
            : previous.FirstOrDefault(p => p.Connection == BrightnessConnection.External && p.DeviceName == id);
    }

    private static (int Minimum, int Current, int Maximum)? ReadExternal(nint monitor)
    {
        return MonitorBrightness.TryRead(monitor, out var min, out var current, out var max)
            ? (min, current, max)
            : null;
    }

    public string? Set(string deviceName, int brightness)
    {
        var monitor = _monitors.FirstOrDefault(m => m.DeviceName == deviceName);
        if (monitor is null)
            return "the monitor is gone";
        try
        {
            return monitor.Connection == BrightnessConnection.Internal
                ? SetWmi(deviceName, brightness)
                : SetDdc(deviceName, brightness);
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
    }

    internal sealed record WmiPanel(string Instance, int Minimum, int Maximum, int Current);

    private List<WmiPanel> ReadWmi()
    {
        var panels = new List<WmiPanel>();
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI",
                "SELECT InstanceName, CurrentBrightness, Level FROM WmiMonitorBrightness");
            using var results = searcher.Get();
            foreach (var monitor in results)
                using (monitor)
                {
                    var instance = monitor["InstanceName"] as string ?? "";
                    var current = Convert.ToInt32(monitor["CurrentBrightness"], CultureInfo.InvariantCulture);
                    var (min, max) = Range(monitor["Level"]);
                    panels.Add(new WmiPanel(instance, min, max, current));
                }
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException
                                       or InvalidCastException)
        {
            WmiFailed("read built-in panel brightness", ex);
        }

        return panels;
    }

    private void WmiFailed(string doing, Exception ex)
    {
        if (ex is ManagementException { ErrorCode: ManagementStatus.NotSupported })
            return;
        log.Warn("power", $"Could not {doing}: {ex.Message}");
    }

    private static string? SetWmi(string instance, int brightness)
    {
        using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
        using var results = searcher.Get();
        foreach (ManagementObject monitor in results)
            using (monitor)
            {
                if (!string.Equals(monitor["InstanceName"] as string, instance, StringComparison.OrdinalIgnoreCase))
                    continue;
                monitor.InvokeMethod("WmiSetBrightness", [1u, (byte)brightness]);
                return null;
            }

        return "Windows no longer lists the panel";
    }

    private static (int Minimum, int Maximum) Range(object? level)
    {
        if (level is not Array array || array.Length == 0)
            return (0, 100);
        var values = new List<int>(array.Length);
        foreach (var item in array)
            values.Add(Convert.ToInt32(item, CultureInfo.InvariantCulture));
        return (values.Min(), values.Max());
    }

    private static string? SetDdc(string deviceName, int brightness)
    {
        if (Screens.Find(deviceName) is not { } screen)
            return "the monitor is gone";
        return MonitorBrightness.TrySet(screen.Handle, (uint)brightness)
            ? null
            : "the monitor did not answer over DDC/CI";
    }
}

internal sealed class BrightnessWriter(Func<string, int, string?> write, Log log, Action? drained = null)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, (int Brightness, Action<string> Failed)> _pending = [];
    private readonly HashSet<string> _running = [];

    public bool IsWriting(string deviceName)
    {
        lock (_lock)
        {
            return _running.Contains(deviceName);
        }
    }

    public void Request(string deviceName, int brightness, Action<string> failed)
    {
        lock (_lock)
        {
            _pending[deviceName] = (brightness, failed);
            if (!_running.Add(deviceName))
                return;
        }

        _ = OwnThread.Run(() => Drain(deviceName));
    }

    private void Drain(string deviceName)
    {
        int? last = null;
        try
        {
            while (true)
            {
                (int Brightness, Action<string> Failed) next;
                lock (_lock)
                {
                    if (!_pending.Remove(deviceName, out next))
                    {
                        _running.Remove(deviceName);
                        break;
                    }
                }

                if (next.Brightness == last)
                    continue;
                last = next.Brightness;
                try
                {
                    if (write(deviceName, next.Brightness) is { } reason)
                        next.Failed(reason);
                }
#pragma warning disable CA1031
                catch (Exception e)
#pragma warning restore CA1031
                {
                    log.Warn("power", e.Message);
                }
            }

            drained?.Invoke();
        }
#pragma warning disable CA1031
        catch (Exception e)
#pragma warning restore CA1031
        {
            lock (_lock)
            {
                _running.Remove(deviceName);
            }

            log.Warn("power", e.Message);
        }
    }
}
