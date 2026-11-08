using Winshell;

namespace OmarchyStyle;

/// <summary>This screen's monitor. A change of screens builds the panels again, so it is read once.</summary>
public sealed class ScreenWidget(WidgetContext context) : StatusWidget(context, "ms-settings:display")
{
    protected override void Show()
    {
        var screen = Bar.Screen(this, Shell);
        if (screen is null)
            return;
        var several = Shell.Screens.Count > 1;
        Bar.Show(this, several ? Glyphs.Screens : Glyphs.Screen, screen.Model.Length > 0 ? screen.Model : screen.Name);
    }
}
