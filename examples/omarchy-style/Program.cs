Shell.Run(shell =>
{
    shell.ExplorerTaskbar.Hidden = true;
    shell.Style = TokyoNight.Style;
    shell.Add(new Bar
    {
        Edge = Edges.Top,
        Start = ["windows", new Placement("workspaces", new Dictionary<string, object> { ["icons"] = true })],
        Center = ["clock"],
        End = ["tray", "bluetooth", "network", "audio", "screen", "battery"]
    });
});
