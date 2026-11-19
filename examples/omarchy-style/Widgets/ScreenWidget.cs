using Microsoft.UI.Xaml;

namespace OmarchyStyle;

public sealed class ScreenWidget : Widget
{
    public override Element Render()
    {
        var screens = UseObservable(Shell.Screens);
        var screen = UseScreen();
        var tip = screen is null ? "" : screen.Model.Length > 0 ? screen.Model : screen.Name;
        return Button(screens.Count > 1 ? Glyphs.Screens : Glyphs.Screen, () => Shell.Open("ms-settings:display"))
            .ToolTip(tip)
            .AutomationName(tip)
            .AutomationId("screen")
            .IsVisible(screen is not null)
            .HAlign(HorizontalAlignment.Left);
    }
}
