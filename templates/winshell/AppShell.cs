using Winshell;

namespace MyShell;

/// <summary>A bar along the top of every screen, with the time in the middle and the battery at the end.</summary>
public sealed class AppShell : ShellConfiguration
{
    public override void Start(Shell shell)
    {
        // "clock" and "battery" are the names these components are registered under.
        Widgets.Register<Clock>("clock");
        Widgets.Register<Battery>("battery");
        _ = new Bar { Edge = Edges.Top, Center = ["clock"], End = ["battery"] };
    }
}
