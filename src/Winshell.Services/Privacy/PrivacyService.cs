using System.ComponentModel;

namespace Winshell.Privacy;

/// <summary>
/// Which apps are using the microphone, the camera or location right now. Windows records each in the capability
/// consent store, and a microphone is also in use while a recording stream is open: current Windows does not always
/// write that stream to the store, and the shell's own audio meter is one such stream.
/// Nothing is read while nothing listens. The first handler starts reading, and removing the last stops it. A change
/// is raised on the loop's thread a tenth of a second after Windows reports it, so one use is one notice. Add and
/// remove handlers on the loop's thread.
/// </summary>
public sealed class PrivacyService : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(100);
    private static readonly IReadOnlyList<PrivacyApp> None = [];

    private readonly IEventLoop _loop;
    private readonly IPrivacyBackend _backend;
    private PropertyChangedEventHandler? _listeners;
    private IDisposable? _settle;
    private IReadOnlyList<PrivacyApp> _microphone = None;
    private IReadOnlyList<PrivacyApp> _camera = None;
    private IReadOnlyList<PrivacyApp> _location = None;
    private bool _microphoneInUse;
    private bool _cameraInUse;
    private bool _locationInUse;
    private bool _watching;
    private bool _disposed;

    internal PrivacyService(IEventLoop loop, Log log, IPrivacyBackend? backend = null)
    {
        _loop = loop;
        _backend = backend ?? new PrivacyBackend(log);
        _backend.Changed += OnChanged;
    }

    /// <summary>The apps using the microphone. Empty while nothing listens, and while none is.</summary>
    public IReadOnlyList<PrivacyApp> Microphone => Reads.Property(this, _microphone);

    /// <summary>Whether any app is using the microphone. False while nothing listens.</summary>
    public bool MicrophoneInUse => Reads.Property(this, _microphoneInUse);

    /// <summary>The apps using the camera. Empty while nothing listens, and while none is.</summary>
    public IReadOnlyList<PrivacyApp> Camera => Reads.Property(this, _camera);

    /// <summary>Whether any app is using the camera. False while nothing listens.</summary>
    public bool CameraInUse => Reads.Property(this, _cameraInUse);

    /// <summary>The apps using location. Empty while nothing listens, and while none is.</summary>
    public IReadOnlyList<PrivacyApp> Location => Reads.Property(this, _location);

    /// <summary>Whether any app is using location. False while nothing listens.</summary>
    public bool LocationInUse => Reads.Property(this, _locationInUse);

    /// <summary>
    /// Raised on the loop's thread when one of the lists or its in-use flag changes. The first handler added starts
    /// reading Windows, and removing the last stops it. Add and remove handlers on the loop's thread.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add
        {
            var first = _listeners is null;
            _listeners += value;
            if (first && _listeners is not null && !_disposed)
                Listen();
        }
        remove
        {
            _listeners -= value;
            if (_listeners is null)
                Quiet();
        }
    }

    /// <summary>Stops reading Windows, and a handler added later does not start it again.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Quiet();
        _backend.Changed -= OnChanged;
        _backend.Dispose();
    }

    private void Listen()
    {
        _watching = true;
        _backend.Start();
        Publish(_backend.Read());
    }

    private void Quiet()
    {
        if (!_watching)
            return;
        _watching = false;
        _settle?.Dispose();
        _settle = null;
        _backend.Stop();
        _microphone = None;
        _camera = None;
        _location = None;
        _microphoneInUse = false;
        _cameraInUse = false;
        _locationInUse = false;
    }

    private void OnChanged()
    {
        if (_disposed || !_watching)
            return;
        _loop.Post(Schedule);
    }

    private void Schedule()
    {
        if (_disposed || !_watching)
            return;
        _settle?.Dispose();
        _settle = _loop.Schedule(Settle, () =>
        {
            _settle = null;
            if (_watching && !_disposed)
                Publish(_backend.Read());
        });
    }

    private void Publish(PrivacySnapshot snapshot)
    {
        var microphone = new List<PrivacyApp>();
        var camera = new List<PrivacyApp>();
        var location = new List<PrivacyApp>();
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in snapshot.Consent)
        {
            if (record.Started == 0 || record.Started <= record.Stopped || record.Identity.Length == 0)
                continue;
            Add(List(record.Capability, microphone, camera, location), index, Key(record),
                FromConsent(record, snapshot));
        }

        foreach (var capture in snapshot.Captures)
        {
            if (capture.Path.Length == 0 && capture.PackageFamilyName.Length == 0)
                continue;
            Add(microphone, index, Key(PrivacyCapability.Microphone, CaptureKey(capture)),
                FromCapture(capture, snapshot));
        }

        Sort(microphone);
        Sort(camera);
        Sort(location);
        Assign(ref _microphone, microphone, ref _microphoneInUse, nameof(Microphone), nameof(MicrophoneInUse));
        Assign(ref _camera, camera, ref _cameraInUse, nameof(Camera), nameof(CameraInUse));
        Assign(ref _location, location, ref _locationInUse, nameof(Location), nameof(LocationInUse));
    }

    private void Assign(ref IReadOnlyList<PrivacyApp> apps, List<PrivacyApp> next, ref bool inUse, string appsName,
        string inUseName)
    {
        var listChanged = !Same(apps, next);
        var useChanged = inUse != next.Count > 0;
        apps = next.Count == 0 ? None : next;
        inUse = next.Count > 0;
        if (listChanged)
            _listeners?.Invoke(this, new PropertyChangedEventArgs(appsName));
        if (useChanged)
            _listeners?.Invoke(this, new PropertyChangedEventArgs(inUseName));
    }

    private static PrivacyApp FromConsent(ConsentRecord record, PrivacySnapshot snapshot)
    {
        var packaged = record.Packaged;
        var path = packaged ? "" : record.Identity.Replace('#', '\\');
        var name = Shown(record.DisplayName, packaged ? record.Identity : FileName(path));
        var mine = packaged
            ? snapshot.PackageFamilyName.Length > 0 &&
              string.Equals(record.Identity, snapshot.PackageFamilyName, StringComparison.OrdinalIgnoreCase)
            : SameProgram(record.Identity, path, snapshot.ProgramPath);
        return new PrivacyApp(name, path, packaged, mine);
    }

    private static PrivacyApp FromCapture(MicrophoneCapture capture, PrivacySnapshot snapshot)
    {
        var packaged = capture.PackageFamilyName.Length > 0;
        var path = packaged ? "" : capture.Path;
        var name = Shown(capture.Name, packaged ? capture.PackageFamilyName : FileName(capture.Path));
        var mine = capture.ThisProgram || SameProgram(capture.Path, capture.Path, snapshot.ProgramPath);
        return new PrivacyApp(name, path, packaged, mine);
    }

    /// <summary>
    /// A full path matches this program's path. A bare file name matches this program's file name: that is all Windows
    /// stored for some desktop apps.
    /// </summary>
    private static bool SameProgram(string identity, string path, string programPath)
    {
        if (programPath.Length == 0)
            return false;
        if (string.Equals(path, programPath, StringComparison.OrdinalIgnoreCase))
            return true;
        if (identity.Contains('#') || identity.Contains('\\') || identity.Contains('/'))
            return false;
        return string.Equals(Path.GetFileName(programPath), identity, StringComparison.OrdinalIgnoreCase);
    }

    private static string Key(ConsentRecord record)
    {
        var id = record.Packaged ? "pkg:" + record.Identity : "exe:" + record.Identity.Replace('#', '\\');
        return Key(record.Capability, id);
    }

    private static string Key(PrivacyCapability capability, string id)
    {
        return capability + "\n" + id;
    }

    private static string CaptureKey(MicrophoneCapture capture)
    {
        return capture.PackageFamilyName.Length > 0
            ? "pkg:" + capture.PackageFamilyName
            : "exe:" + capture.Path;
    }

    private static List<PrivacyApp> List(PrivacyCapability capability, List<PrivacyApp> microphone,
        List<PrivacyApp> camera, List<PrivacyApp> location)
    {
        return capability switch
        {
            PrivacyCapability.Camera => camera,
            PrivacyCapability.Location => location,
            _ => microphone
        };
    }

    private static void Add(List<PrivacyApp> apps, Dictionary<string, int> index, string key, PrivacyApp app)
    {
        if (!index.TryGetValue(key, out var at))
        {
            index[key] = apps.Count;
            apps.Add(app);
            return;
        }

        var prior = apps[at];
        if (prior.ThisProgram || !app.ThisProgram)
            return;
        apps[at] = new PrivacyApp(prior.Name, prior.Path, prior.Packaged, true);
    }

    private static string Shown(string displayName, string fallback)
    {
        var shown = displayName.Trim();
        return shown.Length > 0 ? shown : fallback;
    }

    private static string FileName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.Length > 0 ? name : path;
    }

    private static void Sort(List<PrivacyApp> apps)
    {
        apps.Sort(static (left, right) =>
        {
            var name = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            return name != 0 ? name : string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool Same(IReadOnlyList<PrivacyApp> left, IReadOnlyList<PrivacyApp> right)
    {
        if (left.Count != right.Count)
            return false;
        for (var i = 0; i < left.Count; i++)
            if (left[i].Name != right[i].Name || left[i].Path != right[i].Path ||
                left[i].Packaged != right[i].Packaged || left[i].ThisProgram != right[i].ThisProgram)
                return false;
        return true;
    }
}
