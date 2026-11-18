using System.Runtime.CompilerServices;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;

namespace Winshell;

internal sealed class StyleScope
{
    public required Func<ShellStyle> Style { get; init; }

    public event Action? StyleChanged;

    public event Action? Stopping;

    public bool Stopped { get; private set; }

    public void RaiseStyleChanged() => StyleChanged?.Invoke();

    public void Stop()
    {
        if (Stopped)
            return;
        Stopped = true;
        Stopping?.Invoke();
    }
}

internal static class StyleHost
{
    private sealed class Mark
    {
        public ShellStyle? Style { get; set; }

        public ResourceDictionary? Dictionary { get; set; }
    }

    private static readonly ConditionalWeakTable<FrameworkElement, Mark> Marks = new();

    public static readonly Context<StyleScope?> InEffect = new(null, "ShellStyle");

    public static Func<Action> FollowShellStyle(ShellStyle? own, Action changed) => () =>
    {
        if (own is not null)
            return () => { };
        var shell = Shell.Current;
        shell.StyleChanged += changed;
        return () => shell.StyleChanged -= changed;
    };

    public static Func<Action> Follow(StyleScope? scope, Action changed) => () =>
    {
        if (scope is null)
            return FollowShellStyle(null, changed)();
        scope.StyleChanged += changed;
        return () => scope.StyleChanged -= changed;
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
