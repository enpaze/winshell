using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Winshell;
using static Microsoft.UI.Reactor.Factories;

namespace OmarchyStyle;

public sealed class ScreenWidget : Component<WidgetContext>
{
    public override Element Render()
    {
        if (Props.Window is null)
            return TextBlock("").IsVisible(false).AutomationId("screen");
        return Component<Glyph, GlyphProps>(new(Props.Window, Props));
    }

    private sealed record GlyphProps(ShellWindow Window, WidgetContext Context);

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
                .AutomationId("screen")
                .IsVisible(screen is not null)
                .HAlign(HorizontalAlignment.Left);
        }
    }
}
