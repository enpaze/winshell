using System.Diagnostics;
using System.IO.Pipes;

namespace Winshell;

internal sealed class ShellInstance
{
    private readonly InstanceLog _file;
    private readonly Instances _instances;
    private readonly string _dir;
    private readonly string _shellId;
    private readonly string _program;
    private NamedPipeServerStream? _listening;
    private IpcServer? _server;
    private Exception? _fatal;

    private ShellInstance(ShellLocations locations, string id, string shellName, string program, Log log)
    {
        Id = id;
        _instances = locations.Instances;
        _dir = _instances.DirOf(id);
        Directory.CreateDirectory(_dir);
        _shellId = shellName;
        _program = program;
        Log = log;
        Logs = Path.Combine(_dir, InstanceLog.FileName);
        Pipe = $"winshell-{id}";
        _file = new InstanceLog(Logs);
        Crashes = new CrashReports(locations.CrashRoot, log, shellName, program, Logs);
    }

    public string Id { get; }

    public string Logs { get; }

    private Log Log { get; }

    public CrashReports Crashes { get; }

    public string Pipe { get; private set; }

    public static string ExitState => Path.Combine(ShellLocations.Current.StateRoot, "last-exit.toml");

    public Action? Crashing { get; set; }

    public static ShellInstance Open(string shellName, string program, Log log, bool verbose = false)
    {
        var locations = ShellLocations.Current;
        locations.Instances.Clean();
        var instance = new ShellInstance(locations, Instances.NewId(), shellName, program, log);
        log.WriteTo(instance.WriteLine, verbose ? LogLevel.Debug : LogLevel.Info);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Quietly(() => instance.Crashing?.Invoke());
            if (e.ExceptionObject is Exception error)
                instance.ReportFatal(error);
            else
                Quietly(() => log.Write(LogLevel.Fatal, "", $"Winshell crashed: {e.ExceptionObject}"));
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Quietly(() => log.Error("shell", $"A background task failed: {e.Exception}"));
            e.SetObserved();
            instance.Reported(instance.Crashes.WriteOnce("background task", e.Exception,
                recovery: "A background task failed. Winshell is still running."));
        };
        return instance;
    }

    public string? OpenPipe()
    {
        var pipe = Pipe;
        try
        {
            _listening = IpcServer.OpenAvailable(ref pipe);
        }
        catch (IOException e)
        {
            Log.Error("shell", e.Message);
            return e.Message;
        }

        if (!string.Equals(pipe, Pipe, StringComparison.Ordinal))
            Log.Warn("shell", $"The control pipe {Pipe} was taken. Listening on {pipe}.");
        Pipe = pipe;
        return null;
    }

    public void Listen(IEventLoop loop, IInstanceControl control)
    {
        _server = new IpcServer(_listening!, Pipe, loop, Log, control);
        _listening = null;
    }

    public void Publish()
    {
        _file.Flush();
        using var process = Process.GetCurrentProcess();
        _instances.Write(new InstanceInfo(
            Id,
            Environment.ProcessId,
            _shellId,
            _program,
            TimeProvider.System.GetLocalNow().LocalDateTime,
            Pipe,
            Logs,
            process.StartTime.ToUniversalTime().Ticks,
            ProcessName: process.ProcessName));
    }

    public void ReportFatal(Exception error)
    {
        if (ReferenceEquals(_fatal, error))
            return;
        _fatal = error;
        var report = Crashes.Write("Winshell", error, true);
        Quietly(() => Log.Write(LogLevel.Fatal, "", $"Winshell crashed: {error}"));
        Reported(report);
    }

    public void Reported(CrashReport? report)
    {
        if (report is not null)
            Quietly(() => Log.Info("shell", $"Wrote a crash report to \"{report.Path}\""));
    }

    public void MarkCleanExit()
    {
        if (_fatal is null)
            Quietly(() => CrashReports.MarkClean(ExitState, TimeProvider.System.GetLocalNow()));
    }

    public void Close()
    {
        _server?.Dispose();
        _listening?.Dispose();
        _listening = null;
        _instances.Remove(Id);
        _file.Dispose();
    }

    public void Abandon()
    {
        _listening?.Dispose();
        _listening = null;
        _file.Dispose();
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    internal static void Quietly(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
        }
    }

    private void WriteLine(LogLevel level, string line)
    {
        try
        {
            _file.Write(level,
                $"{TimeProvider.System.GetLocalNow():yyyy-MM-dd HH:mm:ss.fff} {CrashRedaction.Redact(line)}");
        }
        catch (IOException)
        {
        }
    }
}
