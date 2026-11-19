using System.ComponentModel;

namespace Winshell.Privacy;

public sealed class PrivacyService : INotifyPropertyChanged, IDisposable
{
    private static readonly IReadOnlyList<PrivacyApp> None = [];

    private readonly IPrivacyBackend _backend;
    private readonly PropertyListeners _listeners;
    private readonly SettleTimer _settle;
    private IReadOnlyList<PrivacyApp> _microphone = None;
    private IReadOnlyList<PrivacyApp> _camera = None;
    private IReadOnlyList<PrivacyApp> _location = None;
    private bool _microphoneInUse;
    private bool _cameraInUse;
    private bool _locationInUse;
    private bool _watching;

    internal PrivacyService(IEventLoop loop, Log log, IPrivacyBackend? backend = null)
    {
        _backend = backend ?? new PrivacyBackend(log);
        _listeners = new PropertyListeners(this, Listen, Quiet);
        _settle = new SettleTimer(loop, () => Publish(_backend.Read()), () => _watching);
        _backend.Changed += _settle.Signal;
    }

    public IReadOnlyList<PrivacyApp> Microphone => _microphone;

    public bool MicrophoneInUse => _microphoneInUse;

    public IReadOnlyList<PrivacyApp> Camera => _camera;

    public bool CameraInUse => _cameraInUse;

    public IReadOnlyList<PrivacyApp> Location => _location;

    public bool LocationInUse => _locationInUse;

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => _listeners.Add(value);
        remove => _listeners.Remove(value);
    }

    void IDisposable.Dispose()
    {
        if (_listeners.Disposed)
            return;
        _listeners.Dispose();
        _backend.Changed -= _settle.Signal;
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
        _settle.Cancel();
        _backend.Stop();
        _microphone = None;
        _camera = None;
        _location = None;
        _microphoneInUse = false;
        _cameraInUse = false;
        _locationInUse = false;
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
            Add(microphone, index, Key(PrivacyCapability.Microphone, capture.Key),
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
            _listeners.Raise(appsName);
        if (useChanged)
            _listeners.Raise(inUseName);
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
