using System.ComponentModel;
using Winshell.Privacy;

namespace Winshell.Services.Tests;

public sealed class PrivacyServiceTests
{
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(100);

    private const string ShellPath = @"C:\shell\Shell.exe";
    private const string ShellFamily = "Shell.Package_publisher";

    private static void Ignore(object? sender, PropertyChangedEventArgs e)
    {
    }

    [Fact]
    public void Packaged_and_desktop_apps_are_named_from_what_Windows_stored()
    {
        var loop = new ManualEventLoop();
        var backend = new FakePrivacyBackend();
        backend.Consent.Add(new ConsentRecord(PrivacyCapability.Camera, true,
            "Microsoft.WindowsCamera_8wekyb3d8bbwe", 20, 0, "Camera"));
        backend.Consent.Add(new ConsentRecord(PrivacyCapability.Microphone, false,
            @"C:#Windows#System32#SoundRecorder.exe", 30, 10, ""));
        backend.Consent.Add(new ConsentRecord(PrivacyCapability.Location, false,
            @"C:#Apps#Maps.exe", 5, 5, "Maps"));
        backend.Consent.Add(new ConsentRecord(PrivacyCapability.Location, false, "brave.exe", 8, 0, ""));
        backend.Consent.Add(new ConsentRecord(PrivacyCapability.Location, false, "Shell.exe", 9, 0, ""));
        using var privacy = new PrivacyService(loop, new Log(), backend);
        privacy.PropertyChanged += Ignore;

        var camera = Assert.Single(privacy.Camera);
        Assert.Equal("Camera", camera.Name);
        Assert.Equal("", camera.Path);
        Assert.True(camera.Packaged);
        Assert.False(camera.ThisProgram);
        Assert.True(privacy.CameraInUse);

        var microphone = Assert.Single(privacy.Microphone);
        Assert.Equal("SoundRecorder", microphone.Name);
        Assert.Equal(@"C:\Windows\System32\SoundRecorder.exe", microphone.Path);
        Assert.False(microphone.Packaged);
        Assert.False(microphone.ThisProgram);
        Assert.True(privacy.MicrophoneInUse);

        Assert.Equal(2, privacy.Location.Count);
        var brave = Assert.Single(privacy.Location, app => app.Name == "brave");
        Assert.Equal("brave.exe", brave.Path);
        Assert.False(brave.Packaged);
        Assert.False(brave.ThisProgram);
        var shellFile = Assert.Single(privacy.Location, app => app.ThisProgram);
        Assert.Equal("Shell", shellFile.Name);
        Assert.Equal("Shell.exe", shellFile.Path);
        Assert.DoesNotContain(privacy.Location, app => app.Name == "Maps");
        Assert.True(privacy.LocationInUse);
        Assert.Null(loop.NextDue);
    }

    [Fact]
    public void The_shell_s_own_microphone_stream_is_marked_and_listed_once()
    {
        var loop = new ManualEventLoop();
        var backend = new FakePrivacyBackend { ProgramPath = ShellPath, PackageFamilyName = ShellFamily };
        backend.Captures.Add(new MicrophoneCapture("Meter", ShellPath, "", true));
        backend.Captures.Add(new MicrophoneCapture("Meet", @"C:\Apps\Meet.exe", "", false));
        using var privacy = new PrivacyService(loop, new Log(), backend);
        privacy.PropertyChanged += Ignore;

        Assert.Equal(2, privacy.Microphone.Count);
        var shell = Assert.Single(privacy.Microphone, app => app.ThisProgram);
        Assert.Equal("Meter", shell.Name);
        Assert.Equal(ShellPath, shell.Path);
        Assert.False(shell.Packaged);
        var meet = Assert.Single(privacy.Microphone, app => app.Name == "Meet");
        Assert.False(meet.ThisProgram);
        Assert.Equal(@"C:\Apps\Meet.exe", meet.Path);

        backend.Consent.Add(new ConsentRecord(PrivacyCapability.Microphone, false,
            @"C:#shell#Shell.exe", 40, 0, "Shell"));
        backend.Raise();
        loop.Advance(Settle);
        Assert.Equal(2, privacy.Microphone.Count);
        shell = Assert.Single(privacy.Microphone, app => app.ThisProgram);
        Assert.Equal("Shell", shell.Name);
        Assert.Equal(ShellPath, shell.Path);

        backend.Consent.Add(new ConsentRecord(PrivacyCapability.Camera, true, ShellFamily, 12, 0, ""));
        backend.Raise();
        loop.Advance(Settle);
        var packaged = Assert.Single(privacy.Camera);
        Assert.Equal(ShellFamily, packaged.Name);
        Assert.True(packaged.Packaged);
        Assert.True(packaged.ThisProgram);
        Assert.Equal("", packaged.Path);
    }

    [Fact]
    public void Windows_is_read_only_while_something_listens()
    {
        var loop = new ManualEventLoop();
        var backend = new FakePrivacyBackend();
        using var privacy = new PrivacyService(loop, new Log(), backend);
        PropertyChangedEventHandler second = Ignore;

        Assert.Empty(privacy.Microphone);
        Assert.False(privacy.MicrophoneInUse);
        loop.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(0, backend.Reads);
        Assert.Equal(0, backend.Starts);
        Assert.Null(loop.NextDue);

        privacy.PropertyChanged += Ignore;
        privacy.PropertyChanged += second;
        Assert.Equal(1, backend.Starts);
        Assert.Equal(1, backend.Reads);
        loop.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(1, backend.Reads);
        Assert.Null(loop.NextDue);

        privacy.PropertyChanged -= second;
        Assert.Equal(0, backend.Stops);
        privacy.PropertyChanged -= Ignore;
        Assert.Equal(1, backend.Stops);
        Assert.Empty(privacy.Microphone);
        Assert.False(privacy.CameraInUse);
        loop.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(1, backend.Reads);
        Assert.Null(loop.NextDue);

        privacy.PropertyChanged += Ignore;
        Assert.Equal(2, backend.Starts);
        Assert.Equal(2, backend.Reads);
    }

    [Fact]
    public void A_change_is_reported_on_the_loop_once_and_stopping_clears_it()
    {
        var loop = new ManualEventLoop();
        var backend = new FakePrivacyBackend();
        backend.Captures.Add(new MicrophoneCapture("Meet", @"C:\Apps\Meet.exe", "", false));
        using var privacy = new PrivacyService(loop, new Log(), backend);
        List<string?> heard = [];
        privacy.PropertyChanged += (_, e) => heard.Add(e.PropertyName);
        Assert.Equal([nameof(PrivacyService.Microphone), nameof(PrivacyService.MicrophoneInUse)], heard);
        Assert.True(privacy.MicrophoneInUse);
        var reads = backend.Reads;

        heard.Clear();
        backend.Captures.Clear();
        backend.Raise();
        loop.Process();
        Assert.True(privacy.MicrophoneInUse);
        Assert.Empty(heard);
        Assert.Equal(reads, backend.Reads);

        loop.Advance(Settle);
        Assert.Equal([nameof(PrivacyService.Microphone), nameof(PrivacyService.MicrophoneInUse)], heard);
        Assert.Empty(privacy.Microphone);
        Assert.False(privacy.MicrophoneInUse);
        Assert.Equal(reads + 1, backend.Reads);

        heard.Clear();
        backend.Raise();
        loop.Advance(Settle);
        Assert.Empty(heard);
        Assert.Equal(reads + 2, backend.Reads);
        Assert.Null(loop.NextDue);
    }

    [Fact]
    public void Disposing_stops_the_reading()
    {
        var loop = new ManualEventLoop();
        var backend = new FakePrivacyBackend();
        var privacy = new PrivacyService(loop, new Log(), backend);
        privacy.PropertyChanged += Ignore;
        Assert.Equal(1, backend.Starts);

        privacy.Dispose();
        privacy.PropertyChanged += Ignore;
        backend.Raise();
        loop.Advance(Settle);
        Assert.Equal(1, backend.Starts);
        Assert.Equal(1, backend.Reads);
        Assert.Equal(1, backend.Stops);
        Assert.Null(loop.NextDue);
        Assert.False(privacy.MicrophoneInUse);
    }

    private sealed class FakePrivacyBackend : IPrivacyBackend
    {
        public List<ConsentRecord> Consent { get; } = [];

        public List<MicrophoneCapture> Captures { get; } = [];

        public string ProgramPath { get; set; } = ShellPath;

        public string PackageFamilyName { get; set; } = "";

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
