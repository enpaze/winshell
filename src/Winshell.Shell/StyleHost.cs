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
    private const string ShownStyle = "Winshell.ShownStyle";

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
        var merged = element.Resources.MergedDictionaries;
        var shown = merged.FirstOrDefault(dictionary => dictionary.ContainsKey(ShownStyle));
        if (Equals(shown?[ShownStyle], style))
            return false;

        if (shown is not null)
            merged.Remove(shown);
        if (style is null)
            return true;
        var dictionary = Shell.LoadStyle(style, alert);
        dictionary[ShownStyle] = style;
        merged.Insert(0, dictionary);
        return true;
    }
}
