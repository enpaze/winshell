using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using Windows.Management.Deployment;
using Microsoft.Win32;
using Winshell.Native;

namespace Winshell.Privacy;

internal enum PrivacyCapability
{
    Microphone,
    Camera,
    Location
}

/// <summary>One app Windows has a consent record for, whether or not it is using the capability now.</summary>
internal readonly record struct ConsentRecord(
    PrivacyCapability Capability,
    bool Packaged,
    string Identity,
    ulong Started,
    ulong Stopped,
    string DisplayName);

/// <summary>A process with a recording stream open. <see cref="PackageFamilyName"/> is empty for a desktop program.</summary>
internal readonly record struct MicrophoneCapture(string Name, string Path, string PackageFamilyName, bool ThisProgram);

/// <summary>What Windows reports right now, plus who this program is, so the service can mark its own use.</summary>
internal sealed record PrivacySnapshot(
    IReadOnlyList<ConsentRecord> Consent,
    IReadOnlyList<MicrophoneCapture> Captures,
    string ProgramPath,
    string PackageFamilyName);

internal interface IPrivacyBackend : IDisposable
{
    /// <summary>Windows reported a change. Raised on any thread, and possibly several times for one change.</summary>
    event Action? Changed;

    /// <summary>Starts watching. Does nothing the second time.</summary>
    void Start();

    /// <summary>Stops watching. A later <see cref="Start"/> watches again.</summary>
    void Stop();

    /// <summary>Reads the consent store and the open recording streams.</summary>
    PrivacySnapshot Read();
}

/// <summary>
/// Where Windows records capability use, and the recording streams it no longer always writes there.
/// The consent store is <c>CapabilityAccessManager\ConsentStore</c>: packaged apps are subkeys of
/// <c>microphone</c>, <c>webcam</c> and <c>location</c>, and desktop apps are under <c>NonPackaged</c>
/// (a path with <c>#</c> for <c>\</c>, or a file name under <c>Executables</c>). An app is in use when its
/// <c>LastUsedTimeStart</c> is later than <c>LastUsedTimeStop</c>. No supported API lists those apps.
/// <c>AppCapability</c> only answers whether this process may use a capability.
/// A change to the store signals <c>RegNotifyChangeKeyValue</c> on each capability key, including its subkeys.
/// On current Windows a recording stream does not update that store, so open capture sessions are read too, and a
/// session being created or changing state signals the same way. Neither watch is armed until <see cref="Start"/>.
/// </summary>
internal sealed class PrivacyBackend : IPrivacyBackend
{
    private const string Store = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    private static readonly (string Name, PrivacyCapability Capability)[] Capabilities =
    [
        ("microphone", PrivacyCapability.Microphone),
        ("webcam", PrivacyCapability.Camera),
        ("location", PrivacyCapability.Location)
    ];

    private readonly Log _log;
    private readonly List<RegistryKey> _keys = [];
    private readonly Dictionary<string, string> _packageNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly AutoResetEvent _event = new(false);
    private readonly Lock _gate = new();
    private readonly MicrophoneWatch _microphones;
    private RegisteredWaitHandle? _wait;
    private bool _watching;
    private bool _disposed;
    private int _loggedStore;
    private int _loggedName;

    internal PrivacyBackend(Log log)
    {
        _log = log;
        _microphones = new MicrophoneWatch(log, Signal);
    }

    public event Action? Changed;

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || _watching)
                return;
            _watching = true;
            foreach (var (name, _) in Capabilities)
            {
                var key = Registry.CurrentUser.OpenSubKey($@"{Store}\{name}");
                if (key is not null)
                    _keys.Add(key);
            }

            if (_keys.Count > 0 && Arm())
                _wait = ThreadPool.RegisterWaitForSingleObject(
                    _event, (_, _) => OnStore(), null, Timeout.Infinite, false);
        }

        _microphones.Start();
    }

    public void Stop()
    {
        List<RegistryKey> keys;
        RegisteredWaitHandle? wait;
        lock (_gate)
        {
            if (!_watching)
                return;
            _watching = false;
            wait = _wait;
            _wait = null;
            keys = [.. _keys];
            _keys.Clear();
        }

        wait?.Unregister(null);
        foreach (var key in keys)
            key.Dispose();
        _microphones.Stop();
    }

    public PrivacySnapshot Read()
    {
        return new PrivacySnapshot(ReadStore(), _microphones.Read(), Environment.ProcessPath ?? "",
            PackageIdentity.FamilyName((uint)Environment.ProcessId));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        Stop();
        _microphones.Dispose();
        _event.Dispose();
    }

    private List<ConsentRecord> ReadStore()
    {
        var records = new List<ConsentRecord>();
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(Store);
            if (root is null)
                return records;
            foreach (var (name, capability) in Capabilities)
            {
                using var key = root.OpenSubKey(name);
                if (key is not null)
                    Collect(key, capability, true, records);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            if (Interlocked.Exchange(ref _loggedStore, 1) == 0)
                _log.Warn("privacy", $"The capability consent store could not be read: {e.Message}");
        }

        return records;
    }

    private void Collect(RegistryKey key, PrivacyCapability capability, bool packaged, List<ConsentRecord> records)
    {
        foreach (var name in key.GetSubKeyNames())
        {
            using var child = key.OpenSubKey(name);
            if (child is null)
                continue;
            if (name.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Executables", StringComparison.OrdinalIgnoreCase))
            {
                Collect(child, capability, false, records);
                continue;
            }

            var started = Time(child, "LastUsedTimeStart");
            var stopped = Time(child, "LastUsedTimeStop");
            var display = started != 0 && started > stopped ? DisplayName(name, packaged) : "";
            records.Add(new ConsentRecord(capability, packaged, name, started, stopped, display));
        }
    }

    private string DisplayName(string identity, bool packaged)
    {
        try
        {
            if (!packaged)
            {
                var path = identity.Replace('#', '\\');
                return FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim() is { Length: > 0 } description
                    ? description
                    : "";
            }

            if (_packageNames.TryGetValue(identity, out var cached))
                return cached;
            var resolved = PackageDisplayName(identity);
            _packageNames[identity] = resolved;
            return resolved;
        }
        catch (Exception e) when (e is FileNotFoundException or IOException or UnauthorizedAccessException
                                      or ArgumentException or COMException)
        {
            if (Interlocked.Exchange(ref _loggedName, 1) == 0)
                _log.Warn("privacy", $"An app using a capability has no name: {e.Message}");
            return "";
        }
    }

    private static string PackageDisplayName(string familyName)
    {
        foreach (var package in new PackageManager().FindPackagesForUser("", familyName))
        {
            foreach (var app in package.GetAppListEntries())
            {
                var name = app.DisplayInfo.DisplayName?.Trim();
                if (name is { Length: > 0 } && !name.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
                    return name;
            }

            var packageName = package.DisplayName?.Trim();
            if (packageName is { Length: > 0 } &&
                !packageName.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
                return packageName;
        }

        return "";
    }

    private static ulong Time(RegistryKey key, string name)
    {
        return key.GetValue(name) switch
        {
            long value => (ulong)value,
            int value => (uint)value,
            byte[] { Length: >= 8 } bytes => BitConverter.ToUInt64(bytes, 0),
            _ => 0
        };
    }

    private bool Arm()
    {
        var armed = false;
        foreach (var key in _keys)
            armed |= RegistryChange.Notify(key.Handle, _event.SafeWaitHandle);
        return armed;
    }

    private void OnStore()
    {
        lock (_gate)
        {
            if (_disposed || !_watching)
                return;
            Arm();
        }

        Signal();
    }

    private void Signal()
    {
        Changed?.Invoke();
    }
}
