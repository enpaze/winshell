using System.Runtime.CompilerServices;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;

namespace Winshell;

internal static class StyleHost
{
    private sealed class Mark
    {
        public ShellStyle? Style { get; set; }

        public ResourceDictionary? Dictionary { get; set; }
    }

    private static readonly ConditionalWeakTable<FrameworkElement, Mark> Marks = new();

    public static readonly Context<ShellStyle?> InEffect = new(null, "ShellStyle");

    public static Func<Action> FollowShellStyle(ShellStyle? own, Action changed) => () =>
    {
        if (own is not null)
            return () => { };
        var shell = Shell.Current;
        shell.StyleChanged += changed;
        return () => shell.StyleChanged -= changed;
    };

    public static bool Show(FrameworkElement element, ShellStyle? own) =>
        Swap(element, own ?? Shell.Current.Style, alert: false);

    public static void ShowAlert(FrameworkElement element, ShellStyle? style) => Swap(element, style, alert: true);

    private static bool Swap(FrameworkElement element, ShellStyle? style, bool alert)
    {
        var mark = Marks.GetOrCreateValue(element);
        if (Equals(mark.Style, style))
            return false;

        if (mark.Dictionary is not null)
            element.Resources.MergedDictionaries.Remove(mark.Dictionary);
        mark.Style = style;
        mark.Dictionary = style is null ? null : Shell.LoadStyle(style, alert);
        if (mark.Dictionary is not null)
            element.Resources.MergedDictionaries.Insert(0, mark.Dictionary);
        return true;
    }
}
