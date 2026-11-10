using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winshell;
using static Microsoft.UI.Reactor.Factories;

namespace OmarchyStyle;

/// <summary>
/// One glyph can depend on every item of a list. <see cref="RenderContext.UseObservable"/> on the list hears that
/// the list changed, not that an item's own property did, so the item asks the widget to render again.
/// </summary>
internal sealed class Observe : ObservableObject
{
    private int _version;

    public void Bump() => Set(ref _version, _version + 1);

    public static Element Of(INotifyPropertyChanged source, Observe observe, string key, Func<string> stamp) =>
        Component<OnChange, OnChangeProps>(new(source, observe, stamp)).WithKey(key);

    /// <summary>
    /// The widget's own control, with the observers beside it. They take no space. A widget with none is the control
    /// itself, so a button stays the slot the style sizes.
    /// </summary>
    public static Element Show(Element content, HorizontalAlignment alignment, params Element[] observers)
    {
        if (observers.Length == 0)
            return content;
        return Grid(
                columns: [GridSize.Auto],
                rows: [GridSize.Auto],
                content.Grid(0, 0),
                VStack(0, observers).Grid(0, 0).Height(0).IsHitTestVisible(false))
            .HAlign(alignment);
    }
}

internal sealed record OnChangeProps(INotifyPropertyChanged Source, Observe Observe, Func<string> Stamp);

internal sealed class OnChange : Component<OnChangeProps>
{
    public override Element Render()
    {
        _ = UseObservable(Props.Source);
        var stamp = Props.Stamp();
        var first = UseRef(true);
        UseEffect(() =>
        {
            if (first.Current)
            {
                first.Current = false;
                return () => { };
            }

            Props.Observe.Bump();
            return () => { };
        }, stamp);
        return Empty();
    }
}

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
