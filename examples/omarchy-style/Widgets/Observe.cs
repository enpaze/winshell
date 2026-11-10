using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winshell;
using static Microsoft.UI.Reactor.Factories;

namespace OmarchyStyle;

/// <summary>The shell window a widget is drawn in. It is null until the widget is in its bar's window.</summary>
internal static class LoadedWindow
{
    /// <summary>
    /// A visible slot with no size, so the bar can load it and report its window. A collapsed element may never load.
    /// </summary>
    public static Element Placeholder(Action<ShellWindow> set) =>
        TextBlock("").Height(0).OnMount(element => Read(element, set));

    public static void Read(FrameworkElement element, Action<ShellWindow> set)
    {
        void Apply()
        {
            if (ShellWindow.Of(element) is { } window)
                set(window);
        }

        if (element.IsLoaded)
            Apply();
        else
            element.Loaded += (_, _) => Apply();
    }
}

internal static class BarLayout
{
    public static StackElement Along(Orientation orientation, double spacing, params Element?[] children) =>
        orientation == Orientation.Vertical ? VStack(spacing, children) : HStack(spacing, children);

    public static string Key(object value) =>
        RuntimeHelpers.GetHashCode(value).ToString(CultureInfo.InvariantCulture);
}
