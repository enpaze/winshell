using Winshell;

namespace MyShell;

/// <summary>A bar along the top of every screen, with the time in the middle and the battery at the end.</summary>
public sealed class AppShell : ShellConfiguration
{
    public override void Start(Shell shell)
    {
        // "clock" and "battery" are the names MyShell.csproj registers the widgets under.
        _ = new Bar { Edge = Edges.Top, Center = ["clock"], End = ["battery"] };
    }
}
