using Winshell;

namespace MyShell;

public sealed class AppShell : ShellConfiguration
{
    public override void Start(Shell shell)
    {
        Widgets.Register<Clock>("clock");
        Widgets.Register<Battery>("battery");
        _ = new Bar { Edge = Edges.Top, Center = ["clock"], End = ["battery"] };
    }
}
