using System.ComponentModel;
using Winshell.Privacy;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class PrivacyServiceTests
{
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(100);

    private const string ShellPath = @"C:\shell\Shell.exe";
    private const string ShellFamily = "Shell.Package_publisher";

    private static void Ignore(object? sender, PropertyChangedEventArgs e)
    {
    }

    [TestMethod]
    public void Packaged_and_desktop_apps_are_named_from_what_Windows_stored()
    {
        var loop = new ManualEventLoop();
        var backend = new FakePrivacyBackend();
        backend.Consent.Add(new ConsentRecord(PrivacyCapability.Camera, true,
            "Microsoft.WindowsCamera_8wekyb3d8bbwe", 20, 0, "Camera"));
        backend.Consent.Add(new ConsentRecord(PrivacyCapability.Microphone, false,
            "C:#Windows#System32#SoundRecorder.exe", 30, 10, ""));
        backend.Consent.Add(new ConsentRecord(PrivacyCapability.Location, false,
            "C:#Apps#Maps.exe", 5, 5, "Maps"));
        backend.Consent.Add(new ConsentRecord(PrivacyCapability.Location, false, "brave.exe", 8, 0, ""));
        backend.Consent.Add(new ConsentRecord(PrivacyCapability.Location, false, "Shell.exe", 9, 0, ""));
        var privacy = new PrivacyService(loop, new Log(), backend);
        using var stopPrivacy = privacy.Stopping();
        privacy.PropertyChanged += Ignore;

        var camera = Assert.ContainsSingle(privacy.Camera);
        Assert.AreEqual("Camera", camera.Name);
        Assert.AreEqual("", camera.Path);
        Assert.IsTrue(camera.Packaged);
        Assert.IsFalse(camera.ThisProgram);
        Assert.IsTrue(privacy.CameraInUse);

        var microphone = Assert.ContainsSingle(privacy.Microphone);
        Assert.AreEqual("SoundRecorder", microphone.Name);
        Assert.AreEqual(@"C:\Windows\System32\SoundRecorder.exe", microphone.Path);
        Assert.IsFalse(microphone.Packaged);
        Assert.IsFalse(microphone.ThisProgram);
        Assert.IsTrue(privacy.MicrophoneInUse);

        Assert.AreEqual(2, privacy.Location.Count);
        var brave = Assert.ContainsSingle(app => app.Name == "brave", privacy.Location);
        Assert.AreEqual("brave.exe", brave.Path);
        Assert.IsFalse(brave.Packaged);
        Assert.IsFalse(brave.ThisProgram);
        var shellFile = Assert.ContainsSingle(app => app.ThisProgram, privacy.Location);
        Assert.AreEqual("Shell", shellFile.Name);
        Assert.AreEqual("Shell.exe", shellFile.Path);
        Assert.DoesNotContain(app => app.Name == "Maps", privacy.Location);
        Assert.IsTrue(privacy.LocationInUse);
        Assert.IsNull(loop.NextDue);
    }

    [TestMethod]
    public void The_shell_s_own_microphone_stream_is_marked_and_listed_once()
    {
        var loop = new ManualEventLoop();
        var backend = new FakePrivacyBackend { ProgramPath = ShellPath, PackageFamilyName = ShellFamily };
        backend.Captures.Add(new MicrophoneCapture("Meter", ShellPath, "", true));
        backend.Captures.Add(new MicrophoneCapture("Meet", @"C:\Apps\Meet.exe", "", false));
        var privacy = new PrivacyService(loop, new Log(), backend);
        using var stopPrivacy = privacy.Stopping();
        privacy.PropertyChanged += Ignore;

        Assert.AreEqual(2, privacy.Microphone.Count);
        var shell = Assert.ContainsSingle(app => app.ThisProgram, privacy.Microphone);
        Assert.AreEqual("Meter", shell.Name);
        Assert.AreEqual(ShellPath, shell.Path);
        Assert.IsFalse(shell.Packaged);
        var meet = Assert.ContainsSingle(app => app.Name == "Meet", privacy.Microphone);
        Assert.IsFalse(meet.ThisProgram);
        Assert.AreEqual(@"C:\Apps\Meet.exe", meet.Path);

        backend.Consent.Add(new ConsentRecord(PrivacyCapability.Microphone, false,
            "C:#shell#Shell.exe", 40, 0, "Shell"));
        backend.Raise();
        loop.Advance(Settle);
        Assert.AreEqual(2, privacy.Microphone.Count);
        shell = Assert.ContainsSingle(app => app.ThisProgram, privacy.Microphone);
        Assert.AreEqual("Shell", shell.Name);
        Assert.AreEqual(ShellPath, shell.Path);

        backend.Consent.Add(new ConsentRecord(PrivacyCapability.Camera, true, ShellFamily, 12, 0, ""));
        backend.Raise();
        loop.Advance(Settle);
        var packaged = Assert.ContainsSingle(privacy.Camera);
        Assert.AreEqual(ShellFamily, packaged.Name);
        Assert.IsTrue(packaged.Packaged);
        Assert.IsTrue(packaged.ThisProgram);
        Assert.AreEqual("", packaged.Path);
    }

    [TestMethod]
    public void Windows_is_read_only_while_something_listens()
    {
        var loop = new ManualEventLoop();
        var backend = new FakePrivacyBackend();
        var privacy = new PrivacyService(loop, new Log(), backend);
        using var stopPrivacy = privacy.Stopping();
        PropertyChangedEventHandler second = Ignore;

        Assert.IsEmpty(privacy.Microphone);
        Assert.IsFalse(privacy.MicrophoneInUse);
        loop.Advance(TimeSpan.FromSeconds(5));
        Assert.AreEqual(0, backend.Reads);
        Assert.AreEqual(0, backend.Starts);
        Assert.IsNull(loop.NextDue);

        privacy.PropertyChanged += Ignore;
        privacy.PropertyChanged += second;
        Assert.AreEqual(1, backend.Starts);
        Assert.AreEqual(1, backend.Reads);
        loop.Advance(TimeSpan.FromSeconds(5));
        Assert.AreEqual(1, backend.Reads);
        Assert.IsNull(loop.NextDue);

        privacy.PropertyChanged -= second;
        Assert.AreEqual(0, backend.Stops);
        privacy.PropertyChanged -= Ignore;
        Assert.AreEqual(1, backend.Stops);
        Assert.IsEmpty(privacy.Microphone);
        Assert.IsFalse(privacy.CameraInUse);
        loop.Advance(TimeSpan.FromSeconds(5));
        Assert.AreEqual(1, backend.Reads);
        Assert.IsNull(loop.NextDue);

        privacy.PropertyChanged += Ignore;
        Assert.AreEqual(2, backend.Starts);
        Assert.AreEqual(2, backend.Reads);
    }

    [TestMethod]
    public void A_change_is_reported_on_the_loop_once_and_stopping_clears_it()
    {
        var loop = new ManualEventLoop();
        var backend = new FakePrivacyBackend();
        backend.Captures.Add(new MicrophoneCapture("Meet", @"C:\Apps\Meet.exe", "", false));
        var privacy = new PrivacyService(loop, new Log(), backend);
        using var stopPrivacy = privacy.Stopping();
        List<string?> heard = [];
        privacy.PropertyChanged += (_, e) => heard.Add(e.PropertyName);
        Assert.AreSequenceEqual([nameof(PrivacyService.Microphone), nameof(PrivacyService.MicrophoneInUse)], heard);
        Assert.IsTrue(privacy.MicrophoneInUse);
        var reads = backend.Reads;

        heard.Clear();
        backend.Captures.Clear();
        backend.Raise();
        loop.Process();
        Assert.IsTrue(privacy.MicrophoneInUse);
        Assert.IsEmpty(heard);
        Assert.AreEqual(reads, backend.Reads);

        loop.Advance(Settle);
        Assert.AreSequenceEqual([nameof(PrivacyService.Microphone), nameof(PrivacyService.MicrophoneInUse)], heard);
        Assert.IsEmpty(privacy.Microphone);
        Assert.IsFalse(privacy.MicrophoneInUse);
        Assert.AreEqual(reads + 1, backend.Reads);

        heard.Clear();
        backend.Raise();
        loop.Advance(Settle);
        Assert.IsEmpty(heard);
        Assert.AreEqual(reads + 2, backend.Reads);
        Assert.IsNull(loop.NextDue);
    }

    [TestMethod]
    public void Disposing_stops_the_reading()
    {
        var loop = new ManualEventLoop();
        var backend = new FakePrivacyBackend();
        var privacy = new PrivacyService(loop, new Log(), backend);
        privacy.PropertyChanged += Ignore;
        Assert.AreEqual(1, backend.Starts);

        privacy.Stop();
        privacy.PropertyChanged += Ignore;
        backend.Raise();
        loop.Advance(Settle);
        Assert.AreEqual(1, backend.Starts);
        Assert.AreEqual(1, backend.Reads);
        Assert.AreEqual(1, backend.Stops);
        Assert.IsNull(loop.NextDue);
        Assert.IsFalse(privacy.MicrophoneInUse);
    }

    private sealed class FakePrivacyBackend : IPrivacyBackend
    {
        public List<ConsentRecord> Consent { get; } = [];

        public List<MicrophoneCapture> Captures { get; } = [];

        public string ProgramPath { get; init; } = ShellPath;

        public string PackageFamilyName { get; init; } = "";

        public int Reads { get; private set; }

        public int Starts { get; private set; }

        public int Stops { get; private set; }

        private bool _started;

        public event Action? Changed;

        public void Start()
        {
            if (_started)
                return;
            _started = true;
            Starts++;
        }

        public void Stop()
        {
            if (!_started)
                return;
            _started = false;
            Stops++;
        }

        public PrivacySnapshot Read()
        {
            Reads++;
            return new PrivacySnapshot([.. Consent], [.. Captures], ProgramPath, PackageFamilyName);
        }

        public void Raise()
        {
            Changed?.Invoke();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
