using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Winshell;

namespace OmarchyStyle;

/// <summary>Glyphs, tooltips and the screen a bar widget is drawn on.</summary>
internal static class Bar
{
    /// <summary>
    /// The screen of the window <paramref name="element"/> is drawn in. Null until it has been loaded into one.
    /// A window that names no screen is on the primary.
    /// </summary>
    public static ScreenInfo? Screen(UIElement element, Shell shell)
    {
        var window = ShellWindow.Of(element);
        if (window is null)
            return null;
        return window.Screen ?? shell.Screens.FirstOrDefault(screen => screen.IsPrimary);
    }

    /// <summary>
    /// A resource on <paramref name="from"/> or one of its parents, including a dictionary merged into one of them.
    /// An element's own dictionary does not search its parents, and <c>ContainsKey</c> does not search dictionaries it merged.
    /// </summary>
    public static T Resource<T>(FrameworkElement from, object key)
    {
        for (FrameworkElement? element = from; element is not null; element = element.Parent as FrameworkElement)
            if (TryGet(element.Resources, key, out var value))
                return (T)value!;
        throw new KeyNotFoundException($"No resource '{key}' above {from.GetType().Name}.");
    }

    public static void Show(Button button, string glyph, string toolTip, bool alert = false)
    {
        button.Content = glyph;
        button.Foreground = Resource<Brush>(button, alert ? "PanelAlert" : "PanelForeground");
        AutomationProperties.SetName(button, toolTip);
        Tip(button, toolTip);
    }

    /// <summary>Sets <paramref name="element"/>'s tooltip, in the panel's style the first time.</summary>
    /// <param name="scope">Where to find <c>PanelToolTip</c> when <paramref name="element"/> is not in the bar yet.</param>
    public static void Tip(FrameworkElement element, string text, FrameworkElement? scope = null)
    {
        if (ToolTipService.GetToolTip(element) is ToolTip tip)
            tip.Content = text;
        else
            ToolTipService.SetToolTip(element, new ToolTip
            {
                Content = text,
                Style = Resource<Style>(scope ?? element, "PanelToolTip")
            });
    }

    private static bool TryGet(ResourceDictionary dictionary, object key, out object? value)
    {
        if (dictionary.TryGetValue(key, out value))
            return true;
        foreach (var merged in dictionary.MergedDictionaries)
            if (TryGet(merged, key, out value))
                return true;
        return false;
    }

    public static void OpenSettings(string page)
    {
        Process.Start(new ProcessStartInfo(page) { UseShellExecute = true })?.Dispose();
    }
}
