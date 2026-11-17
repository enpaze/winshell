using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;
using Winshell.ExplorerTaskbar;

namespace Winshell;

public sealed record ShellOptions
{
    public string? ShellId { get; init; }

    public string? DataDir { get; init; }

    public string? StateDir { get; init; }

    public string? CacheDir { get; init; }
}

public abstract class ShellConfiguration
{
    public abstract void Start(Shell shell);
}

public sealed partial class Shell : IDisposable
{
    private static Shell? _current;

    private readonly ShellIdentity _identity;
    private readonly ShellInstance _instance;
    private readonly ShellArguments _arguments;
    private bool _nextStarted;
    private bool _disposed;
    private RealInput? _input;

    private Shell(IEventLoop loop, ShellOptions options, InstanceSession session)
    {
        var log = session.Log;
        Loop = loop;
        Log = log;
        Options = options;
        _identity = session.Identity;
        _instance = session.Instance;
        _arguments = session.Arguments;
        LogPath = session.Instance.LogPath;
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

    public ShellOptions Options { get; }

    internal ShellPlatform Platform { get; }

    private PreferencesFile? _preferencesFile;
    private bool _configurationStarted;

    internal PreferencesFile PreferencesFile => OpenPreferences();

    private PreferencesFile OpenPreferences()
    {
        if (_preferencesFile is null)
        {
            _preferencesFile = new PreferencesFile(Path.Combine(ConfigRoot, "preferences", ShellId + ".toml"), Loop, Log);
            if (_configurationStarted)
                _preferencesFile.StartReturned();
        }

        return _preferencesFile;
    }

    private void ConfigurationStarted()
    {
        if (_configurationStarted)
            return;
        _configurationStarted = true;
        _preferencesFile?.StartReturned();
    }

    internal RealInput Input => _input ??= new RealInput(Loop);

    internal ShellNotices Notices { get; }

    public static string Version => ProductVersion.Current.Text;

    public string ShellId => _identity.ShellId;

    public ShellIpc Ipc { get; }

    public string ConfigDir => Path.GetDirectoryName(Environment.ProcessPath)!;

    public string ConfigRoot => ShellLocations.Current.ConfigRoot;

    public string LogPath { get; }

    public string DataDir => Ensure(_identity.DataDir);

    public string StateDir => Ensure(_identity.StateDir);

    public string CacheDir => Ensure(_identity.CacheDir);

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

    public string ConfigPath(string path)
    {
        return Path.Combine(ConfigDir, path);
    }

    public string DataPath(string path)
    {
        return Path.Combine(DataDir, path);
    }

    public string StatePath(string path)
    {
        return Path.Combine(StateDir, path);
    }

    public string CachePath(string path)
    {
        return Path.Combine(CacheDir, path);
    }

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

    private static string Ensure(string dir)
    {
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static void Run<T>(ShellOptions? options = null)
        where T : ShellConfiguration, new()
    {
        Run(new T(), options ?? new ShellOptions());
    }

    private static void Run(ShellConfiguration configuration, ShellOptions options)
    {
        var code = InstanceStart.Run(new InstanceLaunch(
                Environment.ProcessPath!,
                (string[])[.. Environment.GetCommandLineArgs().Skip(1)],
                options.ShellId,
                options.DataDir,
                options.StateDir,
                options.CacheDir,
                Log.Default,
                instance => instance.Crashing = () => _current?.ReleaseForCrash()),
            session => RunHost(configuration, options, session));
        if (code != 0)
            Environment.ExitCode = code;
    }

    private static void RunHost(ShellConfiguration configuration, ShellOptions options, InstanceSession session)
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
                    var loop = new DispatcherLoop(queue, session.Log);
                    loop.Failed += e => instance.Reported(instance.Crashes.WriteOnce("event loop", e, "Winshell",
                        "Something in Winshell failed. It is still running."));
                    shell = new Shell(loop, options, session);
                    _current = shell;
                    if (Environment.GetEnvironmentVariable(DevProjectVariable) is { Length: > 0 })
                        shell.Notices.FollowFileErrors();

                    session.Serve(loop, new Control(shell));
                    Console.CancelKeyPress += (_, e) =>
                    {
                        e.Cancel = true;
                        loop.Post(shell.Quit);
                    };
                    configuration.Start(shell);
                    shell.ConfigurationStarted();
                    shell.ReleaseUnclaimedTaskbar();
                    ShellInstance.Quietly(() => shell.Notices.ShowPrevious(ShellInstance.ExitState));
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    if (shell is null)
                        ExplorerTaskbarLease.ReleaseUnclaimed();
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
        ExplorerTaskbarLease.ReleaseUnclaimed();
    }

    private void Yield()
    {
        _explorerTaskbar?.Yield();
    }

    private void ReleaseForCrash()
    {
        _explorerTaskbar?.Restore();
        PanelEdge.ReleaseAll();
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
            shell.Yield();
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
