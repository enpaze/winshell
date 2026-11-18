using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;

namespace Winshell;

internal static class StyleHost
{
    private sealed class Mark
    {
        public required ShellStyle Style { get; set; }

        public ResourceDictionary? Dictionary { get; set; }
    }

    private static readonly ConditionalWeakTable<FrameworkElement, Mark> Marks = new();

    public static Func<Action> FollowShellStyle(ShellStyle? own, Action changed) => () =>
    {
        if (own is not null)
            return () => { };
        var shell = Shell.Current;
        shell.StyleChanged += changed;
        return () => shell.StyleChanged -= changed;
    };

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
