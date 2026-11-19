using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Winshell.Winland;

namespace OmarchyStyle;

public sealed class WorkspacesWidget : Widget
{
    private const double IconSize = 12;
    private const double GroupGap = 4;

    private static readonly Dictionary<(string Path, int Size), ImageSource> Loaded = [];

    public override Element Render()
    {
        var screen = UseScreen();
        var winland = UseObservableTree(Shell.Winland);
        var screens = UseObservable(Shell.Screens);
        var monitor = Window is not null && winland.ProcessId > 0 ? winland.MonitorFor(screen) : null;
        if (monitor is null)
            return TextBlock("").IsVisible(false).AutomationId("workspaces");
        var icons = Setting("icons", false);
        var scale = screens.FirstOrDefault(each => each.Id == screen?.Id)?.DevicePixelRatio ?? 1;
        return Buttons(monitor, winland, Orientation, icons, scale);
    }

    private static Element Buttons(WinlandMonitor monitor, WinlandService winland, Orientation orientation,
        bool icons, double scale)
    {
        var vertical = orientation == Orientation.Vertical;
        var own = winland.Workspaces.Where(workspace => workspace.Monitor == monitor && workspace.Id > 0).ToList();
        var buttons = own.Select(workspace => workspace.Id).Concat(Enumerable.Range(1, 5)).Distinct().Order()
            .Select(id =>
            {
                var workspace = own.Find(each => each.Id == id);
                var shown = monitor.ActiveWorkspace?.Id == id;
                var occupied = workspace?.Windows.Count > 0;
                var label = id.ToString(CultureInfo.CurrentCulture);
                var text = shown ? Glyphs.Tiling : label;
                var apps = icons && workspace is not null ? workspace.AppIcons : [];
                var button = apps.Count > 0
                    ? Button(Content(text, apps, orientation, scale), () => monitor.ActivateWorkspace(id))
                        .AutomationName("Workspace " + label)
                        .Margin(vertical ? 0 : GroupGap, vertical ? GroupGap : 0, vertical ? 0 : GroupGap,
                            vertical ? GroupGap : 0)
                    : Button(text, () => monitor.ActivateWorkspace(id)).AutomationName("Workspace " + label);
                return button
                    .MinWidth(20)
                    .Opacity(shown || occupied ? 1 : 0.5);
            })
            .Cast<Element?>()
            .ToArray();
        var row = BarLayout.Along(orientation, 1, buttons);
        return vertical
            ? row.HAlign(HorizontalAlignment.Center).AutomationId("workspaces")
            : row.Margin(0, 0, 1.5, 0).HAlign(HorizontalAlignment.Left).AutomationId("workspaces");
    }

    private static Element Content(string text, IReadOnlyList<string> apps, Orientation orientation, double scale)
    {
        var size = (int)Math.Round(IconSize * scale);
        var parts = apps.Select(path => Icon(path, size)).Prepend(TextBlock(text).WithKey("label"));
        return BarLayout.Along(orientation, GroupGap, [.. parts]).HAlign(HorizontalAlignment.Center);
    }

    private static Element? Icon(string path, int size)
    {
        if (!Loaded.TryGetValue((path, size), out var source))
        {
            source = ShellImage.Load(path, size)?.ToImageSource();
            if (source is not null)
                Loaded[(path, size)] = source;
        }

        if (source is null)
            return null;
        return XamlHost(() => new Image { Width = IconSize, Height = IconSize }, image =>
        {
            ((Image)image).Source = source;
            AutomationProperties.SetAccessibilityView(image, AccessibilityView.Raw);
        }, "app-icon").VAlign(VerticalAlignment.Center).WithKey(path);
    }
}
