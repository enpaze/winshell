using System.ComponentModel;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Winshell;

public abstract class Widget : Component<WidgetContext>
{
    protected Shell Shell => Props.Shell;

    protected ShellWindow? Window => Props.Window;

    protected IReadOnlyDictionary<string, object> Settings => Props.Settings;

    protected Orientation Orientation => Props.Orientation;

    protected ShellStyle Style => Props.Style;

    protected FrameworkElement StylePopup(UIElement content) => Props.StylePopup(content);

    protected T Setting<T>(string key, T fallback)
    {
        if (!Settings.TryGetValue(key, out var value))
            return fallback;
        return TomlKeys.TryConvert(value, typeof(T), out var converted)
            ? (T)converted
            : throw new UnsupportedWidgetException($"{key} must be {TomlKeys.Expected(typeof(T))}.");
    }

    protected DateTime UseNow(TimeSpan every)
    {
        var (_, tick) = UseReducer(0);
        var loop = Shell.Loop;
        UseEffect(() =>
        {
            IDisposable? next = null;
            void Wait() => next = loop.Schedule(every, () =>
            {
                tick(count => count + 1);
                Wait();
            });
            Wait();
            return () => next?.Dispose();
        }, every);
        return DateTime.Now;
    }

    protected ScreenInfo? UseScreen()
    {
        var window = Window;
        var (_, moved) = UseReducer(0);
        UseEffect(() =>
        {
            if (window is null)
                return () => { };
            void Changed(object? sender, PropertyChangedEventArgs args)
            {
                if (args.PropertyName == nameof(ShellWindow.Screen))
                    moved(count => count + 1);
            }

            window.PropertyChanged += Changed;
            return () => window.PropertyChanged -= Changed;
        }, window);
        return window?.Screen;
    }
}
