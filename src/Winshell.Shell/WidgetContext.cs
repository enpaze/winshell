using System.Collections.Immutable;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Winshell;

public sealed record Placement(string Widget, IReadOnlyDictionary<string, object>? Settings = null)
{
    private const string NotAList = "must be a list of tables that each name a widget";
    private const string MissingName = "must name a widget in each of its tables";

    public static implicit operator Placement(string widget) => new(widget);

    public static string Check(object value) => Reason(Tables(value));

    public static ImmutableArray<Placement> Parse(object tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if (Tables(tables) is not { } rows)
            throw new ArgumentException(NotAList, nameof(tables));
        if (!rows.All(NamesWidget))
            throw new ArgumentException(MissingName, nameof(tables));
        return [.. rows.Select(Read)];
    }

    public static IReadOnlyList<IReadOnlyDictionary<string, object?>> Format(ImmutableArray<Placement> placements) =>
        placements.Select(Write).ToArray();

    private static Placement Read(IReadOnlyDictionary<string, object?> table) =>
        new((string)table["widget"]!,
            table.Where(pair => pair.Key != "widget").ToDictionary(pair => pair.Key, pair => pair.Value!));

    private static Dictionary<string, object?> Write(Placement placement)
    {
        var table = new Dictionary<string, object?> { ["widget"] = placement.Widget };
        if (placement.Settings is not null)
            foreach (var (key, value) in placement.Settings)
                table.TryAdd(key, value);
        return table;
    }

    private static string Reason(IReadOnlyList<IReadOnlyDictionary<string, object?>>? tables) =>
        tables is null ? NotAList : tables.All(NamesWidget) ? "" : MissingName;

    private static bool NamesWidget(IReadOnlyDictionary<string, object?> table) =>
        table.GetValueOrDefault("widget") is string name && !string.IsNullOrWhiteSpace(name);

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>>? Tables(object? value)
    {
        switch (value)
        {
            case IReadOnlyList<IReadOnlyDictionary<string, object?>> typed:
                return typed;
            case IEnumerable<object?> items:
                var rows = new List<IReadOnlyDictionary<string, object?>>();
                foreach (var item in items)
                {
                    if (item is not IReadOnlyDictionary<string, object?> table)
                        return null;
                    rows.Add(table);
                }

                return rows;
            default:
                return null;
        }
    }
}

public sealed record WidgetContext
{
    private readonly PopupStyles _popups;

    internal WidgetContext(Shell shell, IReadOnlyDictionary<string, object> settings, Orientation orientation,
        ShellWindow? window, PopupAnchor anchor, string widget)
    {
        Shell = shell;
        Window = window;
        Orientation = orientation;
        Settings = settings;
        Widget = widget;
        _popups = new PopupStyles(anchor);
    }

    public Shell Shell { get; }

    public ShellWindow? Window { get; internal init; }

    public Orientation Orientation { get; internal init; }

    public IReadOnlyDictionary<string, object> Settings { get; internal init; }

    internal string Widget { get; init; }

    public ShellStyle? Style => _popups.Style;

    public FrameworkElement StylePopup(UIElement content)
    {
        ObjectDisposedException.ThrowIf(_popups.Stopped, this);
        return _popups.Wrap(content);
    }

    internal void StopStyling() => _popups.Stop();

    private sealed class PopupStyles
    {
        private readonly PopupAnchor _anchor;
        private readonly List<UserControl> _popups = [];
        private bool _watchingStyle;

        public PopupStyles(PopupAnchor anchor)
        {
            _anchor = anchor;
            anchor.Stopping += Stop;
        }

        public bool Stopped { get; private set; }

        public ShellStyle? Style => _anchor.Style();

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
            _anchor.Stopping -= Stop;
            if (_watchingStyle)
                _anchor.StyleChanged -= ReloadStyles;
            _popups.Clear();
        }

        private void WatchStyle()
        {
            if (_watchingStyle)
                return;
            _anchor.StyleChanged += ReloadStyles;
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

internal sealed class PopupAnchor
{
    public Func<ShellStyle?> Style { get; set; } = () => null;

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

public sealed class UnsupportedWidgetException(string message) : Exception(message);
