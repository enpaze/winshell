Shell.Run(shell =>
{
    Widgets.Register<Clock>("clock");
    Widgets.Register<Battery>("battery");
    _ = new Bar { Edge = Edges.Top, Center = ["clock"], End = ["battery"] };
});
