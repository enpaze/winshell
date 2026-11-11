using System.ComponentModel;
using Winshell.Session;

namespace Winshell.Services.Tests;

/// <summary>The Windows session and sign-in, each over a fake. Nothing here changes this PC.</summary>
public sealed class SessionServiceTests
{
    private readonly List<string> _warnings = [];
    private readonly Log _log;

    public SessionServiceTests()
    {
        _log = new Log { Sink = (_, category, message) => _warnings.Add($"{category}: {message}") };
    }

    private static List<string> Watch(INotifyPropertyChanged source)
    {
        var changes = new List<string>();
        source.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);
        return changes;
    }

    [Fact]
    public void Lock_asks_Windows_once()
    {
        var windows = new FakeSessionBackend(true);

        Assert.True(new SessionService(_log, windows).Lock());

        Assert.Equal(1, windows.Calls);
        Assert.Empty(_warnings);
    }

    [Fact]
    public void Lock_reports_when_Windows_refuses()
    {
        Assert.False(new SessionService(_log, new FakeSessionBackend(false)).Lock());

        Assert.Equal(["session: Windows did not lock the session."], _warnings);
    }

    [Fact]
    public void Each_action_asks_Windows_once()
    {
        var windows = new FakeSessionBackend(true);
        var session = new SessionService(_log, windows);

        Assert.True(session.SignOut());
        Assert.True(session.Restart());
        Assert.True(session.ShutDown());
        Assert.True(session.Sleep());
        Assert.True(session.Hibernate());

        Assert.Equal(["sign out", "restart", "shut down", "sleep", "hibernate"], windows.Actions);
        Assert.Empty(_warnings);
    }

    [Fact]
    public void Each_action_reports_when_Windows_refuses()
    {
        var session = new SessionService(_log, new FakeSessionBackend(false));

        Assert.False(session.SignOut());
        Assert.False(session.Restart());
        Assert.False(session.ShutDown());
        Assert.False(session.Sleep());
        Assert.False(session.Hibernate());

        Assert.Equal([
            "session: Windows did not sign out.",
            "session: Windows did not restart.",
            "session: Windows did not shut down.",
            "session: Windows did not sleep.",
            "session: Windows did not hibernate."
        ], _warnings);
    }

    [Fact]
    public void Sleep_and_hibernate_availability_is_what_Windows_reports()
    {
        var asleep = new FakeSessionBackend(true) { SleepAvailable = true, HibernateAvailable = false };
        var session = new SessionService(_log, asleep);

        Assert.True(session.SleepAvailable);
        Assert.False(session.HibernateAvailable);

        var hibernates = new FakeSessionBackend(true) { SleepAvailable = false, HibernateAvailable = true };
        session = new SessionService(_log, hibernates);

        Assert.False(session.SleepAvailable);
        Assert.True(session.HibernateAvailable);
        Assert.Equal(0, asleep.Calls + hibernates.Calls);
        Assert.Empty(_warnings);
    }

    [Fact]
    public void Nothing_is_registered_until_the_shell_turns_it_on_and_off_removes_it()
    {
        var windows = new FakeRegistry();
        var startup = new StartupService(_log, windows);
        var changes = Watch(startup);

        Assert.True(startup.Available);
        Assert.False(startup.RunAtSignIn);
        Assert.Equal(0, windows.Writes);

        startup.RunAtSignIn = true;
        Assert.True(windows.Registered);
        Assert.True(startup.RunAtSignIn);

        startup.RunAtSignIn = false;
        Assert.False(windows.Registered);
        Assert.Equal(2, windows.Writes);
        Assert.Equal([nameof(StartupService.RunAtSignIn), nameof(StartupService.RunAtSignIn)], changes);
        Assert.Empty(_warnings);
    }

    [Fact]
    public void The_Startup_apps_page_turning_it_off_is_reported_and_left_alone()
    {
        var windows = new FakeRegistry { Registered = true, TurnedOff = true };
        var startup = new StartupService(_log, windows);
        Assert.True(startup.RunAtSignIn);
        Assert.True(startup.TurnedOffInWindows);

        windows.TurnedOff = false;
        startup.Refresh();

        Assert.False(startup.TurnedOffInWindows);
        Assert.Equal(0, windows.Writes);
    }

    [Fact]
    public void A_refused_write_keeps_the_state_Windows_has_and_warns()
    {
        var startup = new StartupService(_log, new FakeRegistry { Accepts = false }) { RunAtSignIn = true };

        Assert.False(startup.RunAtSignIn);
        Assert.Equal(["startup: Windows did not let Winshell register to run at sign-in."], _warnings);
    }

    [Fact]
    public void An_entry_that_starts_another_winshell_is_not_this_one_running_at_sign_in()
    {
        var windows = new FakeRegistry { Registered = true, Elsewhere = true };
        var startup = new StartupService(_log, windows);

        Assert.True(startup.RegisteredElsewhere);
        Assert.False(startup.RunAtSignIn);

        startup.RunAtSignIn = true;

        Assert.Equal(1, windows.Writes);
        Assert.False(startup.RegisteredElsewhere);
        Assert.True(startup.RunAtSignIn);
    }

    [Fact]
    public void Without_a_registry_it_is_unavailable_and_registers_nothing()
    {
        var startup = new StartupService(_log, null) { RunAtSignIn = true };

        Assert.False(startup.Available);
        Assert.False(startup.RunAtSignIn);
        Assert.Single(_warnings);
    }

    private sealed class FakeSessionBackend(bool accepts) : ISessionBackend
    {
        public int Calls { get; private set; }

        public List<string> Actions { get; } = [];

        public bool SleepAvailable { get; init; }

        public bool HibernateAvailable { get; init; }

        public bool Lock()
        {
            return Answer("lock");
        }

        public bool SignOut()
        {
            return Answer("sign out");
        }

        public bool Restart()
        {
            return Answer("restart");
        }

        public bool ShutDown()
        {
            return Answer("shut down");
        }

        public bool Sleep()
        {
            return Answer("sleep");
        }

        public bool Hibernate()
        {
            return Answer("hibernate");
        }

        private bool Answer(string action)
        {
            Calls++;
            Actions.Add(action);
            return accepts;
        }
    }

    private sealed class FakeRegistry : IStartupRegistry
    {
        public bool Registered { get; set; }

        public bool TurnedOff { get; set; }

        public bool Elsewhere { get; set; }

        public bool Accepts { get; init; } = true;

        public int Writes { get; private set; }

        public SignInVerdict Read()
        {
            return new SignInVerdict
            {
                HasEntry = Registered,
                PointsElsewhere = Elsewhere,
                OffInWindows = TurnedOff,
                Kind = !Registered ? SignInKind.None
                    : Elsewhere ? SignInKind.AnotherInstall
                    : TurnedOff ? SignInKind.TurnedOffInWindows
                    : SignInKind.Registered
            };
        }

        public bool Register()
        {
            if (!Accepts)
                return false;
            Writes++;
            Registered = true;
            Elsewhere = false;
            return true;
        }

        public bool Unregister()
        {
            if (!Accepts)
                return false;
            Writes++;
            Registered = false;
            return true;
        }
    }
}
