using System.Runtime.CompilerServices;
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
        for (DependencyObject? node = start; node is not null; node = ParentOf(node))
        {
            if (node is FrameworkElement element && Marks.TryGetValue(element, out var mark))
                return Equals(mark.Style, active);
        }

        return false;
    }

    /// <summary>The host that renders <paramref name="start"/>, when this element is already in its tree.</summary>
    public static ReactorHostControl? FindHost(DependencyObject start)
    {
        for (DependencyObject? node = start; node is not null; node = ParentOf(node))
            if (node is ReactorHostControl host)
                return host;
        return null;
    }

    /// <summary>
    /// Puts <paramref name="own"/> where <paramref name="element"/>'s children inherit it: on the host, when nothing
    /// above the element has a style yet, and on the element when an ancestor shows a different one. A later call
    /// replaces the dictionary already placed. <paramref name="target"/> remembers that element. Returns whether the
    /// resolved style changed.
    /// </summary>
    public static bool Place(FrameworkElement element, ShellStyle? own, ref FrameworkElement? target)
    {
        var resolved = own ?? Shell.Current.Style;
        // The first placement can run before the element is in the host. Once it is, a dictionary that landed on
        // the element itself moves onto the host, or comes off when an ancestor already shows this style.
        if (target is not null && ReferenceEquals(target, element) && FindHost(element) is { } host &&
            !ReferenceEquals(host, element) && ParentOf(element) is { } above)
        {
            if (Matches(above, resolved))
            {
                Clear(element);
                return false;
            }

            if (Carrier(above) is null)
            {
                Clear(element);
                Apply(host, own);
                target = host;
                return true;
            }
        }

        if (target is not null)
        {
            if (Marks.TryGetValue(target, out var mark) && Equals(mark.Style, resolved))
                return false;
            // The element is borrowing an ancestor's dictionary. A style that still matches leaves that dictionary
            // where it is; a different one is this element's own.
            if (!Marks.TryGetValue(target, out _) && Matches(element, resolved))
                return false;
            Apply(target, own);
            return true;
        }

        if (Matches(element, resolved))
        {
            target = element;
            return false;
        }

        var where = Carrier(element) is null ? FindHost(element) ?? element : element;
        Apply(where, own);
        target = where;
        return true;
    }

    private static void Clear(FrameworkElement element)
    {
        if (!Marks.TryGetValue(element, out var mark))
            return;
        if (mark.Dictionary is not null)
            element.Resources.MergedDictionaries.Remove(mark.Dictionary);
        Marks.Remove(element);
    }

    private static FrameworkElement? Carrier(DependencyObject start)
    {
        for (DependencyObject? node = start; node is not null; node = ParentOf(node))
            if (node is FrameworkElement element && Marks.TryGetValue(element, out _))
                return element;
        return null;
    }

    /// <summary>
    /// The logical parent, which is set as soon as the element is content, before it is in a window. The visual parent
    /// is only a fallback: it stays null until the element is connected, which would leave a style on the element
    /// instead of the host.
    /// </summary>
    private static DependencyObject? ParentOf(DependencyObject node) =>
        node is FrameworkElement { Parent: { } parent } ? parent : VisualTreeHelper.GetParent(node);
}
