using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;

namespace Winshell;

/// <summary>
/// The style dictionary on an element. It is inserted first, so a dictionary merged later wins, and replacing the
/// style removes only this dictionary. Each element gets its own dictionary: a resource dictionary has one parent.
/// </summary>
internal static class StyleHost
{
    private sealed class Mark
    {
        public ShellStyle? Style { get; set; }

        public ResourceDictionary? Dictionary { get; set; }
    }

    private static readonly ConditionalWeakTable<FrameworkElement, Mark> Marks = new();

    /// <summary>
    /// Shows <paramref name="own"/>, or the shell's style when it is null, on <paramref name="element"/>.
    /// Returns whether that resolved style changed.
    /// </summary>
    public static bool Show(FrameworkElement element, ShellStyle? own)
    {
        var resolved = own ?? Shell.Current.Style;
        if (Marks.TryGetValue(element, out var mark) && Equals(mark.Style, resolved))
            return false;

        var next = Shell.LoadStyle(resolved);
        if (mark is null)
        {
            Marks.Add(element, new Mark { Style = resolved, Dictionary = next });
            element.Resources.MergedDictionaries.Insert(0, next);
            return true;
        }

        if (mark.Dictionary is not null)
            element.Resources.MergedDictionaries.Remove(mark.Dictionary);
        mark.Style = resolved;
        mark.Dictionary = next;
        element.Resources.MergedDictionaries.Insert(0, next);
        return true;
    }
}
