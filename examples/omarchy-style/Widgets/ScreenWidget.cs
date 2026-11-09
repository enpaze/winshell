using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Winshell;

namespace OmarchyStyle;

/// <summary>The monitor this bar is on, and whether there are others.</summary>
public sealed class ScreenWidget : Button
{
    public ScreenWidget(WidgetContext context)
    {
        var shell = context.Shell;
        this.Watch(() =>
        {
            // Null until the widget is in its bar's window. The watch runs again when it loads.
            var screen = ShellWindow.Of(this)?.Screen;
            if (screen is null)
                return;
            var tip = screen.Model.Length > 0 ? screen.Model : screen.Name;
            Content = shell.Screens.Count > 1 ? Glyphs.Screens : Glyphs.Screen;
            ToolTipService.SetToolTip(this, tip);
            AutomationProperties.SetName(this, tip);
        });
        Click += (_, _) => SettingsPage.Open("ms-settings:display");
    }
}
