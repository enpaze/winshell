using Winshell.Native;

namespace Winshell;

internal enum NoticeKind
{
    BuildError,

    ConfigError,

    Crash
}

internal sealed record Notice(
    NoticeKind Kind,
    string Title,
    string Detail,
    string Body,
    string Note,
    string ActionText,
    string Target,
    TimeSpan Timeout);

internal sealed partial class ShellNotices : IDisposable
{
    public static readonly TimeSpan CrashTimeout = TimeSpan.FromSeconds(25);

    private const int QunsBusy = 2, QunsFullScreen = 3, QunsPresentation = 4, QunsQuietTime = 6;

    private readonly IEventLoop _loop;
    private readonly Log _log;
    private readonly CrashReports _crashes;
    private readonly Func<Notice, Action, Action, IDisposable> _show;
    private readonly Action<string, string?> _open;
    private readonly Func<bool> _isQuiet;
    private IDisposable? _window;
    private bool _disposed;

    public ShellNotices(IEventLoop loop, Log log, CrashReports crashes, Func<Notice, Action, Action, IDisposable> show,
        Action<string, string?> open, Func<bool> isQuiet)
    {
        _loop = loop;
        _log = log;
        _crashes = crashes;
        _show = show;
        _open = open;
        _isQuiet = isQuiet;
        crashes.Written += OnReport;
    }

    public Notice? Current { get; private set; }

    internal static bool QuietState(int state)
    {
        return state is QunsBusy or QunsFullScreen or QunsPresentation or QunsQuietTime;
    }

    internal static bool WindowsIsQuiet()
    {
        return PInvoke.SHQueryUserNotificationState(out var state).Succeeded && QuietState((int)state);
    }

    internal static TimeSpan CrashTimeoutFor(uint messageSeconds)
    {
        return TimeSpan.FromSeconds(Math.Max(CrashTimeout.TotalSeconds, messageSeconds));
    }

    public static Notice ForBuildError(string error, string projectFile)
    {
        return new Notice(
            NoticeKind.BuildError,
            "Your configuration did not build",
            Path.GetFileName(projectFile),
            error,
            "Winshell keeps running the last version that built.",
            "Open project",
            projectFile,
            TimeSpan.Zero);
    }

    private static Notice ForConfigError(string error, string file)
    {
        return new Notice(
            NoticeKind.ConfigError,
            "This file has an error",
            Path.GetFileName(file),
            error,
            "Winshell keeps what it had.",
            "Open file",
            file,
            TimeSpan.Zero);
    }

    public void ShowConfigError(string file, string error)
    {
        if (error.Length > 0)
            Show(ForConfigError(error, file));
        else if (Current is { Kind: NoticeKind.ConfigError } current && current.Target == file)
            Hide();
    }

    public void ShowBuildError(string error, string projectFile)
    {
        if (error.Length > 0)
            Show(ForBuildError(error, projectFile));
        else if (Current is { Kind: NoticeKind.BuildError } current && current.Target == projectFile)
            Hide();
    }

    public static Notice ForCrash(CrashReport report, TimeSpan timeout)
    {
        return ForCrash(report.Subject.Length > 0 ? report.Subject : "a window", report.Recovery, report.Path, timeout);
    }

    private static Notice ForCrash(string subject, string body, string reportPath, TimeSpan timeout)
    {
        var title = char.ToUpperInvariant(subject[0]) + subject[1..] + " stopped working";
        return new Notice(NoticeKind.Crash, title, "", body, "A crash report was saved on this PC.", "Open report",
            reportPath, timeout);
    }

    public void Show(Notice notice)
    {
        if (_disposed)
            return;
        Hide();
        try
        {
            _log.Info("notice", $"{notice.Title}: {notice.Detail}");
            Current = notice;
            _window = _show(notice, Act, () => _loop.Post(Hide));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Current = null;
            _log.Error("notice", $"Could not show a notice: {e.Message}");
        }
    }

    public void Hide()
    {
        Current = null;
        _window?.Dispose();
        _window = null;
    }

    public void ShowPrevious(string stateFile)
    {
        if (CrashReports.Find(_crashes.Folder, stateFile) is not { } closed)
            return;
        CrashReports.MarkShown(stateFile, closed.Path);
        var body = closed.ExceptionLine.Length > 0 ? closed.ExceptionLine : "Winshell closed the last time it ran.";
        var notice = ForCrash("Winshell", body, closed.Path, ReadCrashTimeout());
        _loop.Post(() => Show(notice));
    }

    public void FollowFileErrors()
    {
        _log.FileErrorChanged += ShowConfigError;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _crashes.Written -= OnReport;
        _log.FileErrorChanged -= ShowConfigError;
        Hide();
    }

    private void Act()
    {
        if (Current is not { } notice)
            return;
        _open(notice.Target, notice.Kind == NoticeKind.Crash ? null : "edit");
        _loop.Post(Hide);
    }

    private void OnReport(CrashReport report)
    {
        if (report.Fatal || report.Recovery.Length == 0)
            return;
        _loop.Post(() =>
        {
            if (Current?.Kind == NoticeKind.BuildError)
                return;
            if (_isQuiet())
            {
                _log.Info("shell", $"A crash notice was held back while Windows is quiet. See {report.Path}");
                return;
            }

            Show(ForCrash(report, ReadCrashTimeout()));
        });
    }

    private static TimeSpan ReadCrashTimeout()
    {
        return MessageDuration.Seconds() is { } seconds ? CrashTimeoutFor(seconds) : CrashTimeout;
    }
}
