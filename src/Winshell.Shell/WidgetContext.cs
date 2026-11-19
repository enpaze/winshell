using System.Collections.Immutable;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Winshell;

public sealed record Placement(string Widget, IReadOnlyDictionary<string, object>? Settings = null)
{
    private const string NotPlacements = "must be a list of widget names or tables that each name a widget";

    public static implicit operator Placement(string widget) => new(widget);

    internal static string Check(object value) => Items(value)?.All(Names) == true ? "" : NotPlacements;

    internal static ImmutableArray<Placement> Parse(object items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (Items(items) is not { } list || !list.All(Names))
            throw new ArgumentException(NotPlacements, nameof(items));
        return [.. list.Select(Read)];
    }

    internal static IReadOnlyList<object> Format(ImmutableArray<Placement> placements) =>
        placements.Select(Write).ToArray();

    private static Placement Read(object? item) => new(NameOf(item)!,
        item is IReadOnlyDictionary<string, object?> table
            ? table.Where(pair => pair.Key != "widget").ToDictionary(pair => pair.Key, pair => pair.Value!)
            : new Dictionary<string, object>());

    private static object Write(Placement placement)
    {
        var table = new Dictionary<string, object?> { ["widget"] = placement.Widget };
        if (placement.Settings is not null)
            foreach (var (key, value) in placement.Settings)
                table.TryAdd(key, value);
        return table.Count == 1 ? placement.Widget : table;
    }

    private static bool Names(object? item) => !string.IsNullOrWhiteSpace(NameOf(item));

    private static string? NameOf(object? item) => item switch
    {
        string name => name,
        IReadOnlyDictionary<string, object?> table => table.GetValueOrDefault("widget") as string,
        _ => null
    };

    private static IReadOnlyList<object?>? Items(object? value) =>
        value is IEnumerable<object?> items && !IsSet(value) ? items.ToList() : null;

    private static bool IsSet(object value) => value.GetType().GetInterfaces().Any(type =>
        type.IsGenericType && type.GetGenericTypeDefinition() is var open &&
        (open == typeof(ISet<>) || open == typeof(IReadOnlySet<>)));
}

public sealed record WidgetContext
{
    private readonly PopupStyles _popups;

    internal WidgetContext(Shell shell, IReadOnlyDictionary<string, object> settings, Orientation orientation,
        ShellWindow? window, StyleScope scope, string widget)
    {
        Shell = shell;
        Window = window;
        Orientation = orientation;
        Settings = settings;
        Widget = widget;
        _popups = new PopupStyles(scope);
    }

    public Shell Shell { get; }

    public ShellWindow? Window { get; internal init; }

    public Orientation Orientation { get; internal init; }

    public IReadOnlyDictionary<string, object> Settings { get; internal init; }

    internal string Widget { get; init; }

    public ShellStyle Style => _popups.Style;

    public FrameworkElement StylePopup(UIElement content)
    {
        ObjectDisposedException.ThrowIf(_popups.Stopped, this);
        return _popups.Wrap(content);
    }

    internal void StopStyling() => _popups.Stop();

    private sealed class PopupStyles
    {
        private readonly StyleScope _area;
        private readonly List<UserControl> _popups = [];
        private bool _watchingStyle;

        public PopupStyles(StyleScope scope)
        {
            _area = scope;
            scope.Stopping += Stop;
        }

        public bool Stopped { get; private set; }

        public ShellStyle Style => _area.Style();

        public FrameworkElement Wrap(UIElement content)
        {
            ArgumentNullException.ThrowIfNull(content);
            ObjectDisposedException.ThrowIf(Stopped, this);
            var wrapper = new UserControl { Content = content };
            LoadStyle(wrapper);
            WatchStyle();
            _popups.Add(wrapper);
            return wrapper;
        }

        public void Stop()
        {
            if (Stopped)
                return;
            Stopped = true;
            _area.Stopping -= Stop;
            if (_watchingStyle)
                _area.StyleChanged -= ReloadStyles;
            _popups.Clear();
        }

        private void WatchStyle()
        {
            if (_watchingStyle)
                return;
            _area.StyleChanged += ReloadStyles;
            _watchingStyle = true;
        }

        private void LoadStyle(UserControl wrapper)
        {
            wrapper.Resources.MergedDictionaries.Clear();
            wrapper.Resources.MergedDictionaries.Add(Shell.LoadStyle(Style));
        }

        private void ReloadStyles()
        {
            foreach (var wrapper in _popups)
                LoadStyle(wrapper);
        }
    }
}

public sealed class UnsupportedWidgetException(string message) : Exception(message);
