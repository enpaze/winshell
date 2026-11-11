using System.Runtime.CompilerServices;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Winshell;

/// <summary>
/// The style dictionary on a host or an element. It is inserted first, so a dictionary merged later wins, and replacing
/// the style removes only this dictionary.
/// </summary>
internal static class StyleHost
{
    private sealed class Mark
    {
        public ShellStyle? Style { get; set; }

        public ResourceDictionary? Dictionary { get; set; }
    }

    private static readonly ConditionalWeakTable<FrameworkElement, Mark> Marks = new();

    /// <summary>A host whose content is the component, with the style dictionary already in place so children inherit it.</summary>
    public static ReactorHostControl Create<TComponent, TProps>(TProps props)
        where TComponent : Component<TProps>, new()
    {
        return new ReactorHostControl
        {
            ComponentFactory = () => new TComponent(),
            Props = props,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            IsTabStop = false
        };
    }

    /// <summary>Shows <paramref name="own"/>, or the shell's style when it is null. The resolved style is what a child compares.</summary>
    public static void Apply(FrameworkElement host, ShellStyle? own)
    {
        var resolved = own ?? Shell.Current.Style;
        var next = Shell.LoadStyle(resolved);
        if (!Marks.TryGetValue(host, out var mark))
        {
            Marks.Add(host, new Mark { Style = resolved, Dictionary = next });
            host.Resources.MergedDictionaries.Insert(0, next);
            return;
        }

        if (mark.Dictionary is not null)
            host.Resources.MergedDictionaries.Remove(mark.Dictionary);
        mark.Style = resolved;
        mark.Dictionary = next;
        host.Resources.MergedDictionaries.Insert(0, next);
    }

    /// <summary>Whether an ancestor already shows <paramref name="active"/>, so a child does not load a second dictionary.</summary>
    public static bool Matches(DependencyObject start, ShellStyle? active)
    {
        for (DependencyObject? node = start; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is FrameworkElement element && Marks.TryGetValue(element, out var mark))
                return Equals(mark.Style, active);
        }

        return false;
    }
}
