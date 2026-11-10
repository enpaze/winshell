using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Winshell;
using static Microsoft.UI.Reactor.Factories;

namespace OmarchyStyle;

/// <summary>The monitor this bar is on, and whether there are others.</summary>
public sealed class ScreenWidget : Component<WidgetContext>
{
    public override Element Render()
    {
        var observe = UseRef(new Observe());
        _ = UseObservable(observe.Current);
        var screens = UseObservable(Props.Shell.Screens);
        var (window, setWindow) = UseState<ShellWindow?>(null);
        if (window is null)
            return LoadedWindow.Placeholder(setWindow);

        var screen = window.Screen;
        var observers = new Element[]
        {
            Observe.Of(window, observe.Current, "window", () =>
            {
                var shown = window.Screen;
                return shown is null ? "" : shown.Id + "\n" + shown.Model + "\n" + shown.Name;
            })
        };
        var tip = screen is null ? "" : screen.Model.Length > 0 ? screen.Model : screen.Name;
        var button = Button(screens.Count > 1 ? Glyphs.Screens : Glyphs.Screen, () => Props.Shell.Open("ms-settings:display"))
            .ToolTip(tip)
            .AutomationName(tip)
            .IsVisible(screen is not null);
        return Observe.Show(button, HorizontalAlignment.Left, observers);
    }
}
