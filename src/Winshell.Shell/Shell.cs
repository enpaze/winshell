using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;
using Winshell.ExplorerTaskbar;

namespace Winshell;

public sealed partial class Shell : IDisposable
{
    private static Shell? _current;

    private readonly ShellIdentity _identity;
    private readonly ShellInstance _instance;
    private readonly ShellArguments _arguments;
    private bool _nextStarted;
    private bool _disposed;
    private RealInput? _input;

    private Shell(IEventLoop loop, InstanceSession session, WidgetRegistry widgets)
    {
        var log = session.Log;
        Loop = loop;
        WidgetRegistry = widgets;
        Log = log;
        _identity = session.Identity;
        _instance = session.Instance;
        _arguments = session.Arguments;
        Paths = new ShellPaths(session.Identity, session.Instance.Logs,
            Path.GetDirectoryName(Environment.ProcessPath)!, ShellLocations.Current.ShellsRoot);
        Ipc = new ShellIpc(log);
        Platform = new ShellPlatform(loop, log);
        Notices = new ShellNotices(
            loop,
            log,
            session.Instance.Crashes,
            (notice, act, dismiss) => new NoticeWindow(this, notice, act, dismiss, loop),
            (path, verb) => OpenToRead.Open(path, verb, log),
            ShellNotices.WindowsIsQuiet);
        StartToolTipStyle();
    }

    internal const string DevProjectVariable = "WINSHELL_DEV_PROJECT";

    internal static Shell? Running => _current;

    public static Shell Current =>
        _current ?? throw new InvalidOperationException("The shell has not started. Call Shell.Run first.");

    public IEventLoop Loop { get; }

    public Log Log { get; }

    internal ShellPlatform Platform { get; }

    internal WidgetRegistry WidgetRegistry { get; }

    private PreferencesFile? _preferencesFile;
    private bool _startReturned;

    internal PreferencesFile PreferencesFile
    {
        get
        {
            if (_preferencesFile is null)
            {
                _preferencesFile = new PreferencesFile(Path.Combine(Paths.Shells, "preferences", ShellId + ".toml"), Loop, Log);
                if (_startReturned)
                    _preferencesFile.StartReturned();
            }

            return _preferencesFile;
        }
    }

    private void StartReturned()
    {
        if (_startReturned)
            return;
        _startReturned = true;
        _preferencesFile?.StartReturned();
    }

    internal RealInput Input => _input ??= new RealInput(Loop);

    internal ShellNotices Notices { get; }

    public static string Version => ProductVersion.Current.Text;

    public string ShellId => _identity.ShellId;

    public ShellIpc Ipc { get; }

    public ShellPaths Paths { get; }

    public ObservableList<ScreenInfo> Screens => Platform.Screens;

    public bool IsDark => Platform.IsDark;

    public ColorScheme ColorScheme
    {
        get => Platform.ColorScheme;
        set => Platform.ColorScheme = value;
    }

    public string AccentColor
    {
        get => Platform.AccentColor;
        set => Platform.AccentColor = value;
    }

    public string SystemAccent => Platform.SystemAccent;

    public void Open(string target)
    {
        _ = Task.Run(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
            }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException)
            {
                Log.Warn("shell", $"Could not open {target}: {e.Message}");
            }
        });
    }

    public static void Run(Action<Shell> start)
    {
        ArgumentNullException.ThrowIfNull(start);
        StaThread.Run(() => Start(start));
    }

    private static void Start(Action<Shell> start)
    {
        var code = InstanceStart.Run(new InstanceLaunch(
                Environment.ProcessPath!,
                (string[])[.. Environment.GetCommandLineArgs().Skip(1)],
                ShellProperties.Of(Assembly.GetEntryAssembly()),
                Log.Default,
                instance => instance.Crashing = () => _current?.ReleaseForCrash()),
            session => RunHost(start, session));
        if (code != 0)
            Environment.ExitCode = code;
    }

    private static void RunHost(Action<Shell> start, InstanceSession session)
    {
        var instance = session.Instance;
        Exception? failed = null;
        ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            var queue = DispatcherQueue.GetForCurrentThread();
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(queue));
            var application = new ShellApplication(() =>
            {
                Shell? shell = null;
                try
                {
                    var widgets = WidgetRegistry.Discover(session.Log);
                    var loop = new DispatcherLoop(queue, session.Log);
                    loop.Failed += e => instance.Reported(instance.Crashes.WriteOnce("event loop", e, "Winshell",
                        "Something in Winshell failed. It is still running."));
                    shell = new Shell(loop, session, widgets);
                    _current = shell;
                    if (Environment.GetEnvironmentVariable(DevProjectVariable) is { Length: > 0 })
                        shell.Notices.FollowFileErrors();

                    session.Serve(loop, new Control(shell));
                    Console.CancelKeyPress += (_, e) =>
                    {
                        e.Cancel = true;
                        loop.Post(shell.Quit);
                    };
                    start(shell);
                    shell.StartReturned();
                    shell.ReleaseUnclaimedTaskbar();
                    ShellInstance.Quietly(() => shell.Notices.ShowPrevious(ShellInstance.ExitState));
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    if (shell is null)
                        ExplorerTaskbarLease.ReleaseUnclaimed(ShellLocations.Current);
                    else
                        shell.ReleaseUnclaimedTaskbar();
                    instance.ReportFatal(e);
                    failed = e;
                    queue.TryEnqueue(() =>
                    {
                        ShellInstance.Quietly(() => shell?.Dispose());
                        Application.Current.Exit();
                    });
                }
            });
            application.UnhandledException += (_, e) =>
            {
                ShellInstance.Quietly(() => instance.Crashing?.Invoke());
                instance.ReportFatal(e.Exception);
            };
            GC.KeepAlive(application);
        });
        if (failed is not null)
            session.Failure = failed;
    }

    public bool Reload()
    {
        if (StartNext() is not null)
            return false;
        Quit();
        return true;
    }

    private string? StartNext(string? program = null)
    {
        if (_nextStarted)
            return null;
        program ??= Environment.ProcessPath!;
        var info = new ProcessStartInfo(program) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in _arguments.ForNext(Environment.ProcessId))
            info.ArgumentList.Add(argument);
        try
        {
            using var next = Process.Start(info);
            _nextStarted = true;
            Log.Info("shell", "Reloading: a new process takes over from this one.");
            return null;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            var error = $"Could not start \"{program}\": {e.Message}";
            Log.Error("shell", error);
            return error;
        }
    }

    public void Quit()
    {
        try
        {
            Dispose();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Error("shell", e.ToString());
            _instance.Reported(_instance.Crashes.WriteOnce("teardown", e, "Winshell",
                "Something in Winshell did not stop cleanly. Winshell exited anyway."));
        }
        finally
        {
            Application.Current.Exit();
        }
    }

    private void ReleaseUnclaimedTaskbar()
    {
        if (_explorerTaskbar is { Hidden: true })
            return;
        ExplorerTaskbarLease.ReleaseUnclaimed(ShellLocations.Current);
    }

    private void ReleaseForCrash()
    {
        _explorerTaskbar?.Restore();
        PanelPlacer.ReleaseAll();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        DisposeEach((IDisposable?[])[_input, _preferencesFile, Notices, .. ServicesInStopOrder, Platform]);
    }

    internal static void DisposeEach(IEnumerable<IDisposable?> parts)
    {
        List<Exception> failures = [];
        foreach (var part in parts)
            try
            {
                part?.Dispose();
            }
            catch (Exception e)
            {
                failures.Add(e);
            }

        if (failures.Count == 1)
            ExceptionDispatchInfo.Throw(failures[0]);
        if (failures.Count > 1)
            throw new AggregateException(failures);
    }

    private sealed class Control(Shell shell) : IInstanceControl
    {
        public ShellIpc Ipc => shell.Ipc;

        public string? Reload(string? program)
        {
            return shell.StartNext(program);
        }

        public void Quit()
        {
            shell.Quit();
        }

        public void Yield()
        {
            shell._explorerTaskbar?.Yield();
        }

        public void ShowBuildError(string error, string projectFile)
        {
            shell.Notices.ShowBuildError(error, projectFile);
        }

        public void DismissNotice()
        {
            shell.Notices.Hide();
        }

        public Task<string> Inspect(string command, IReadOnlyList<string> args)
        {
            return Inspector.Run(shell, command, args);
        }
    }
}
