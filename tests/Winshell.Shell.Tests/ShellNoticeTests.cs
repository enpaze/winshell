using System.Text.Json;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Winshell.ShellTests;

[TestClass]
public sealed partial class ShellNoticeTests : IDisposable
{
    private readonly string _dir = TempFolder.NewPath();
    private readonly ManualEventLoop _loop = new();
    private readonly List<string> _logged = [];
    private readonly List<Notice> _shown = [];
    private readonly List<(string Path, string? Verb)> _opened = [];
    private readonly Log _log;
    private readonly CrashReports _crashes;
    private readonly ShellNotices _notices;
    private Action _act = () => { };
    private Action _dismiss = () => { };
    private int _closed;
    private bool _quiet;
    private bool _showFails;

    public ShellNoticeTests()
    {
        _crashes = new CrashReports(_dir);
        _log = new Log { Sink = (_, category, message) => _logged.Add($"{category}: {message}") };
        _notices = new ShellNotices(_loop, _log, _crashes, Show, (path, verb) => _opened.Add((path, verb)),
            () => _quiet);
    }

    public void Dispose()
    {
        _notices.Dispose();
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private Closes Show(Notice notice, Action act, Action dismiss)
    {
        if (_showFails)
            throw new InvalidOperationException("no window");
        _shown.Add(notice);
        _act = act;
        _dismiss = dismiss;
        return new Closes(this);
    }

    [TestMethod]
    public void A_file_that_does_not_read_shows_until_it_is_fixed()
    {
        _notices.FollowFileErrors();
        _log.ReportFileError(@"C:\config\preferences.toml", "preferences.toml[3:1]: expected a value");

        var notice = Assert.ContainsSingle(_shown);
        Assert.AreEqual(NoticeKind.FileError, notice.Kind);
        Assert.AreEqual("preferences.toml", notice.Detail);
        Assert.Contains("[3:1]", notice.Body, StringComparison.Ordinal);
        Assert.AreEqual(TimeSpan.Zero, notice.Timeout);

        _log.ReportFileError(@"C:\config\preferences.toml", "");

        Assert.IsNull(_notices.Current);
    }

    [TestMethod]
    public void Fixing_one_file_leaves_the_notice_of_another()
    {
        _notices.ShowFileError(@"C:\config\launcher.toml", "broken");

        _notices.ShowFileError(@"C:\config\preferences.toml", "");

        Assert.IsNotNull(_notices.Current);
    }

    [TestMethod]
    public void A_fixed_file_leaves_a_build_error_on_screen()
    {
        _notices.Show(ShellNotices.ForBuildError("no", "Mine.csproj"));

        _notices.ShowFileError(@"C:\config\preferences.toml", "");

        Assert.AreEqual(NoticeKind.BuildError, _notices.Current?.Kind);
    }

    private sealed partial class Closes(ShellNoticeTests test) : IDisposable
    {
        public void Dispose()
        {
            test._closed++;
        }
    }

    private void Fail(bool fatal = false, string recovery = "Winshell opened it again.")
    {
        if (fatal)
            _crashes.Write("taskbar", new InvalidOperationException("no"), true);
        else
            _crashes.WriteOnce("taskbar", new InvalidOperationException(Guid.NewGuid().ToString()), "the taskbar",
                recovery);
        _loop.Process();
    }

    private static Notice Build()
    {
        return ShellNotices.ForBuildError("Bar.cs(1,1): error CS1002: ; expected", @"C:\mine\Bar.csproj");
    }

    [TestMethod]
    public void A_failure_says_what_stopped_and_what_Winshell_did()
    {
        Fail();

        var notice = Assert.ContainsSingle(_shown);
        Assert.AreEqual(NoticeKind.Crash, notice.Kind);
        Assert.AreEqual("The taskbar stopped working", notice.Title);
        Assert.AreEqual("Winshell opened it again.", notice.Body);
        Assert.AreEqual("Open report", notice.ActionText);
        Assert.IsTrue(notice.Timeout >= ShellNotices.CrashTimeout);
    }

    [TestMethod]
    public void A_failure_that_ends_the_process_or_has_no_recovery_shows_nothing()
    {
        Fail(true);
        Fail(recovery: "");

        Assert.IsEmpty(_shown);
    }

    [TestMethod]
    public void A_previous_close_is_shown_from_the_report()
    {
        var report = _crashes.Write("Winshell", new InvalidOperationException("boom"), true);
        Assert.IsNotNull(report);
        var state = Path.Combine(_dir, "last-exit.toml");

        _notices.ShowPrevious(state);
        _loop.Process();

        var notice = Assert.ContainsSingle(_shown);
        Assert.AreEqual("Winshell stopped working", notice.Title);
        Assert.Contains("boom", notice.Body, StringComparison.Ordinal);
        Assert.AreEqual(report.Path, notice.Target);

        _notices.ShowPrevious(state);
        _loop.Process();

        Assert.ContainsSingle(_shown);
    }

    [TestMethod]
    [DataRow(2, true)]
    [DataRow(3, true)]
    [DataRow(4, true)]
    [DataRow(6, true)]
    [DataRow(5, false)]
    public void Windows_holding_notifications_back_is_read_as_a_toast_reads_it(int state, bool quiet)
    {
        Assert.AreEqual(quiet, ShellNotices.QuietState(state));
    }

    [TestMethod]
    public void A_quiet_Windows_holds_a_failure_back_and_the_log_says_where_it_went()
    {
        _quiet = true;

        Fail();

        Assert.IsEmpty(_shown);
        Assert.Contains(line => line.Contains("held back", StringComparison.Ordinal), _logged);
    }

    [TestMethod]
    public void A_failure_does_not_replace_a_build_error()
    {
        _notices.Show(Build());

        Fail();

        Assert.ContainsSingle(_shown);
        Assert.AreEqual(NoticeKind.BuildError, _notices.Current?.Kind);
    }

    [TestMethod]
    public void A_build_error_replaces_a_failure_and_stays_until_it_is_dismissed()
    {
        Fail();

        _notices.Show(Build());

        Assert.AreEqual(1, _closed);
        Assert.AreEqual(NoticeKind.BuildError, _notices.Current?.Kind);
        Assert.AreEqual(TimeSpan.Zero, _notices.Current?.Timeout);
    }

    [TestMethod]
    public void A_build_error_names_the_project_and_what_keeps_running()
    {
        var notice = Build();

        Assert.AreEqual("Bar.csproj", notice.Detail);
        Assert.AreEqual(@"C:\mine\Bar.csproj", notice.Target);
        Assert.Contains("keeps running", notice.Note, StringComparison.Ordinal);
    }

    [TestMethod]
    public void The_button_opens_the_file_and_closes_the_notice()
    {
        _notices.Show(Build());

        _act();
        _loop.Process();

        Assert.AreSequenceEqual([@"C:\mine\Bar.csproj"], _opened.Select(opened => opened.Path));
        Assert.AreSequenceEqual(["edit"], _opened.Select(opened => opened.Verb));
        Assert.IsNull(_notices.Current);
        Assert.AreEqual(1, _closed);
    }

    [TestMethod]
    public void A_crash_report_opens_with_the_default_verb()
    {
        Fail();
        var report = Assert.ContainsSingle(_shown).Target;

        _act();
        _loop.Process();

        Assert.AreSequenceEqual([report], _opened.Select(opened => opened.Path));
        Assert.AreSequenceEqual([null], _opened.Select(opened => opened.Verb));
    }

    [TestMethod]
    public void A_text_log_opens_to_edit()
    {
        _notices.ShowFileError(@"C:\logs\log.txt", "could not read");

        _act();
        _loop.Process();

        Assert.AreSequenceEqual([@"C:\logs\log.txt"], _opened.Select(opened => opened.Path));
        Assert.AreSequenceEqual(["edit"], _opened.Select(opened => opened.Verb));
    }

    [TestMethod]
    public void Dismissing_closes_it_from_the_loop_and_opens_nothing()
    {
        _notices.Show(Build());

        _dismiss();
        Assert.IsNotNull(_notices.Current);
        _loop.Process();

        Assert.IsNull(_notices.Current);
        Assert.IsEmpty(_opened);
    }

    [TestMethod]
    public void A_notice_that_cannot_show_goes_to_the_log()
    {
        _showFails = true;

        _notices.Show(Build());

        Assert.IsNull(_notices.Current);
        Assert.Contains(line => line.StartsWith("notice: Could not show", StringComparison.Ordinal), _logged);
    }

    [TestMethod]
    [TestCategory("Window")]
    public async Task The_window_sits_above_the_taskbar_at_the_right_of_the_screen_and_says_the_whole_notice()
    {
        var read = await ShellFixture.On(async shell =>
        {
            var notice = Build();
            using var window = new NoticeWindow(shell, notice, () => { }, () => { }, shell.Loop);
            var panel = (PanelWindow)shell.Platform.Windows.Single().Client;
            var root = await Shown(panel);
            return (
                panel.Width,
                panel.Height,
                Name: AutomationProperties.GetName(root),
                Live: AutomationProperties.GetLiveSetting(root));
        });

        Assert.AreEqual(364, read.Width);
        Assert.IsTrue(read.Height > 100);
        Assert.Contains("did not build", read.Name, StringComparison.Ordinal);
        Assert.Contains("CS1002", read.Name, StringComparison.Ordinal);
        Assert.AreEqual(AutomationLiveSetting.Assertive, read.Live);
    }

    [TestMethod]
    [TestCategory("Window")]
    public async Task The_window_is_tall_enough_for_its_button()
    {
        var fits = await ShellFixture.On(async shell =>
        {
            using var window = new NoticeWindow(shell, Build(), () => { }, () => { }, shell.Loop);
            var panel = (PanelWindow)shell.Platform.Windows.Single().Client;
            var root = await Shown(panel);
            var action = ActionButton(root);
            await ShellFixture.LaidOut(action);
            await Wait.UntilAsync(
                () => action.TransformToVisual(root).TransformPoint(default).Y + action.ActualHeight +
                    root.Padding.Bottom <= root.ActualHeight + 0.5,
                "the button to fit in the window");
            return true;
        });

        Assert.IsTrue(fits);
    }

    [TestMethod]
    [TestCategory("Window")]
    public async Task A_timed_window_closes_itself_and_an_untimed_one_waits()
    {
        var dismissed = await ShellFixture.On(shell =>
        {
            var loop = new ManualEventLoop();
            var timed = 0;
            var untimed = 0;
            var crash = ShellNotices.ForCrash(
                new CrashReport("r.txt", "x", new InvalidOperationException("no"), false) { Recovery = "Fine." },
                TimeSpan.FromSeconds(25));
            using var first = new NoticeWindow(shell, crash, () => { }, () => timed++, loop);
            using var second = new NoticeWindow(shell, Build(), () => { }, () => untimed++, loop);
            loop.Advance(TimeSpan.FromSeconds(26));
            return (timed, untimed);
        });

        Assert.AreEqual((1, 0), dismissed);
    }

    [TestMethod]
    [TestCategory("Window")]
    public async Task A_build_failure_notice_is_drawn_by_a_component()
    {
        var type = await ShellFixture.On(async shell =>
        {
            using var window = new NoticeWindow(shell, Build(), () => { }, () => { }, shell.Loop);
            var panel = (PanelWindow)shell.Platform.Windows.Single().Client;
            await Shown(panel);
            var tree = await Inspector.Run(shell, "tree", []);
            return JsonElement.Parse(tree).EnumerateArray().Single()
                .GetProperty("content").GetProperty("type").GetString();
        });

        Assert.AreEqual("NoticeComponent", type);
    }

    private static async Task<Grid> Shown(PanelWindow panel)
    {
        var host = (ReactorHostControl)panel.Content!;
        await ShellFixture.LaidOut(host);
        await Wait.UntilAsync(() => panel.Opacity == 1, "the notice to show");
        await host.WaitForIdleAsync();
        return NoticeRoot(host);
    }

    private static Grid NoticeRoot(DependencyObject node)
    {
        Grid? root = null;
        for (DependencyObject? current = DismissButton(node); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is Grid { Padding.Left: > 0 } grid)
                root = grid;
        }

        return root ?? throw new InvalidOperationException("The notice is not showing.");
    }

    private static Button DismissButton(DependencyObject node) =>
        FindButton(node, name => name == "Dismiss")
        ?? throw new InvalidOperationException("The notice has no dismiss button.");

    private static Button ActionButton(DependencyObject node) =>
        FindButton(node, name => name != "Dismiss")
        ?? throw new InvalidOperationException("The notice has no action button.");

    private static Button? FindButton(DependencyObject current, Func<string, bool> match)
    {
        if (current is Button button)
            return match(AutomationProperties.GetName(button)) ? button : null;
        var count = VisualTreeHelper.GetChildrenCount(current);
        for (var i = 0; i < count; i++)
        {
            if (FindButton(VisualTreeHelper.GetChild(current, i), match) is { } found)
                return found;
        }

        return null;
    }
}
