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
        if (Props.Window is null)
            return TextBlock("").IsVisible(false);
        return Component<Glyph, GlyphProps>(new(Props.Window, Props));
    }

    private sealed record GlyphProps(ShellWindow Window, WidgetContext Context);

    /// <summary>The glyph, once the bar's window is known. The window says which screen this bar is on.</summary>
    private sealed class Glyph : Component<GlyphProps>
    {
        public override Element Render()
        {
            var window = UseObservable(Props.Window);
            var screens = UseObservable(Props.Context.Shell.Screens);
            var screen = window.Screen;
            var tip = screen is null ? "" : screen.Model.Length > 0 ? screen.Model : screen.Name;
            return Button(screens.Count > 1 ? Glyphs.Screens : Glyphs.Screen,
                    () => Props.Context.Shell.Open("ms-settings:display"))
                .ToolTip(tip)
                .AutomationName(tip)
                .IsVisible(screen is not null)
                .HAlign(HorizontalAlignment.Left);
        }
    }
}
