using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Winshell;

/// <summary>
/// A panel along one edge of every screen, with start, center and end widget areas. It is built only from Winshell's
/// public building blocks, so a variant of your own can start as a copy of it.
/// <code>
/// var bar = new Bar { Edge = Edges.Top, Start = ["workspaces"], Center = ["clock"], End = ["battery"] };
/// </code>
/// It shows on the next turn of the loop, once its properties are set, and <see cref="Dispose"/> removes it. A bar
/// reserves its edge and leaves Explorer's taskbar as it is. A left or right edge runs the areas from top to bottom.
/// <para>
/// Its section of the preferences file, named after <see cref="Name"/>, holds <c>edge</c>, <c>start</c>,
/// <c>center</c> and <c>end</c>, which replace the properties of the same names while the file has them. A
/// <c>[[bar.screen]]</c> row, as <see cref="ScreenWindows"/> matches it, may change any of them for one screen. A
/// change to a row replaces that screen's bar, and a change to the section replaces every screen's. A value in the
/// section that can't be used is logged, and the bar keeps the one it had. One in a row is logged under the bar's name,
/// and that screen uses the section's.
/// </para>
/// </summary>
public sealed class Bar : IDisposable
{
    /// <summary>Each edge as the preferences file writes it.</summary>
    private static readonly Dictionary<string, Edges> EdgeNames = new()
    {
        ["top"] = Edges.Top,
        ["bottom"] = Edges.Bottom,
        ["left"] = Edges.Left,
        ["right"] = Edges.Right
    };

    private readonly Shell _shell = Shell.Current;
    private BarPreferences? _preferences;
    private ScreenWindows? _windows;
    private bool _remakePosted;
    private bool _disposed;

    /// <summary>A bar on the top edge with no widgets. Make it on the shell's thread.</summary>
    public Bar()
    {
        _shell.StyleChanged += FollowShellStyle;
        PostRemake();
    }

    /// <summary>The bar's name, which names its section of the preferences file. Give each bar of a shell its own.</summary>
    public string Name
    {
        get;
        init => field = string.IsNullOrEmpty(value)
            ? throw new ArgumentException("A bar needs a name.", nameof(value))
            : value;
    } = "bar";

    /// <summary>The edge the bar runs along: one of <see cref="Edges.Top"/>, Bottom, Left and Right.</summary>
    public Edges Edge
    {
        get;
        init => field = EdgeNames.ContainsValue(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "A bar runs along one edge.");
    } = Edges.Top;

    /// <summary>
    /// The widgets at the start: the left, or the top on a side bar. A plain string names a widget with no settings.
    /// </summary>
    public IReadOnlyList<Placement> Start { get; init; } = [];

    /// <summary>The widgets in the middle.</summary>
    public IReadOnlyList<Placement> Center { get; init; } = [];

    /// <summary>The widgets at the end: the right, or the bottom on a side bar.</summary>
    public IReadOnlyList<Placement> End { get; init; } = [];

    /// <summary>Whether the bar reserves its edge, so maximized windows stop short of it. False lays it over them.</summary>
    public bool Reserve { get; init; } = true;

    /// <summary>
    /// The height of a top or bottom bar, or the width of a side bar, in logical pixels. Null takes the style's
    /// <c>ShellSlotSize</c>.
    /// </summary>
    public double? Thickness { get; init; }

    /// <summary>
    /// The space between the areas and each end of the bar, along its length, in logical pixels: at the left and
    /// right of a top or bottom bar, and at the top and bottom of a side bar. Null takes the style's
    /// <c>ShellPadding</c>.
    /// </summary>
    public double? Padding { get; init; }

    /// <summary>
    /// The source URI of the bar's own style, which wins over <see cref="Shell.Style"/>, or null for the shell's.
    /// </summary>
    public Uri? Style { get; init; }

    private void FollowShellStyle()
    {
        if (Style is null)
            PostRemake();
    }

    // The properties are set after the constructor, and a save can change several keys at once. Making the bars once,
    // on the next turn, sees all of them.
    private void PostRemake()
    {
        if (_remakePosted || _disposed)
            return;
        _remakePosted = true;
        _shell.Loop.Post(() =>
        {
            _remakePosted = false;
            if (_disposed)
                return;
            // Reading the file runs the configuration's handlers, which can dispose this.
            if (_preferences is null)
            {
                _preferences = new BarPreferences(this);
                if (_disposed)
                {
                    _preferences.Dispose();
                    return;
                }

                _preferences.PropertyChanged += (_, _) => PostRemake();
            }

            _windows?.Dispose();
            var windows = new ScreenWindows(Name, Show);
            if (_disposed)
                windows.Dispose();
            else
                _windows = windows;
        });
    }

    /// <summary>Shows the bar on one screen, from the screen's row where it has one and the section elsewhere.</summary>
    private IDisposable Show(ScreenInfo screen, IReadOnlyDictionary<string, object?>? row)
    {
        var section = _preferences!;
        var edge = EdgeNames[(string)Pick("edge", section.Edge)];
        var vertical = edge is Edges.Left or Edges.Right;
        var start = Area("start", section.Start, HorizontalAlignment.Left, VerticalAlignment.Top);
        var center = Area("center", section.Center, HorizontalAlignment.Center, VerticalAlignment.Center);
        var end = Area("end", section.End, HorizontalAlignment.Right, VerticalAlignment.Bottom);
        var shown = new Shown([start, center, end]);
        try
        {
            // The surface paints the style's background behind the areas, in the window's theme.
            var areas = new Grid { Children = { start, center, end } };
            var surface = shown.Surface = new Surface { Style = Style, Content = areas };
            var thickness = Thickness ?? (double)surface.Resources["ShellSlotSize"];
            var padding = Padding ?? (double)surface.Resources["ShellPadding"];
            areas.Padding = vertical ? new Thickness(0, padding, 0, padding) : new Thickness(padding, 0, padding, 0);
            var window = shown.Window = new PanelWindow
            {
                Screen = screen,
                Anchors = edge | (vertical ? Edges.Top | Edges.Bottom : Edges.Left | Edges.Right),
                ReservedSize = Reserve ? thickness : 0,
                Content = surface
            };
            if (vertical)
                window.Width = thickness;
            else
                window.Height = thickness;
            window.Visible = true;
            return shown;
        }
        catch
        {
            // A style without a key the bar reads, say. The areas' widgets go with the bar that couldn't be shown.
            shown.Dispose();
            throw;
        }

        WidgetArea Area(string key, object placements, HorizontalAlignment across, VerticalAlignment down)
        {
            return new WidgetArea
            {
                Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal,
                Style = Style,
                HorizontalAlignment = vertical ? HorizontalAlignment.Stretch : across,
                VerticalAlignment = vertical ? down : VerticalAlignment.Stretch,
                Defaults = Placements(Pick(key, placements))
            };
        }

        // The row's value for a key, when it has one that can be used, or the section's.
        object Pick(string key, object fromSection)
        {
            if (row is null || !row.TryGetValue(key, out var value))
                return fromSection;
            var reason = value is null ? "is empty" : Check(key, value);
            if (reason.Length == 0)
                return value!;
            _shell.Log.Warn(Name, $"{screen.Manufacturer} / {screen.Model} / {screen.SerialNumber}: " +
                                  $"{key} in its [[{Name}.screen]] row {reason}, so the bar uses [{Name}]'s");
            return fromSection;
        }
    }

    /// <summary>Why a value read for a key of the section or of a row is refused, or <c>""</c>.</summary>
    private static string Check(string key, object value)
    {
        if (key == "edge")
            return value is string edge && EdgeNames.ContainsKey(edge)
                ? ""
                : "must be \"top\", \"bottom\", \"left\" or \"right\"";
        return value is IEnumerable<object?> tables && tables.All(table =>
            table is IReadOnlyDictionary<string, object?> settings &&
            settings.GetValueOrDefault("widget") is string widget && !string.IsNullOrWhiteSpace(widget))
            ? ""
            : "must be a list of tables that each name a widget";
    }

    /// <summary>A list of tables as placements: <c>widget</c> names each widget, and the other keys are its settings.</summary>
    private static Placement[] Placements(object tables)
    {
        return
        [
            .. ((IEnumerable<object?>)tables).Cast<IReadOnlyDictionary<string, object?>>().Select(table =>
                new Placement((string)table["widget"]!,
                    table.Where(pair => pair.Key != "widget").ToDictionary(pair => pair.Key, pair => pair.Value!)))
        ];
    }

    /// <summary>Removes the bar from every screen, and stops following the screens, the file and the style.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _shell.StyleChanged -= FollowShellStyle;
        _windows?.Dispose();
        _preferences?.Dispose();
    }

    /// <summary>The bar's section. Each key starts at the bar's property, which a missing key goes back to.</summary>
    private sealed class BarPreferences(Bar bar) : Preferences(bar.Name, Check)
    {
        [TomlComment("top, bottom, left or right.")]
        public string Edge
        {
            get;
            set => Set(ref field, value);
        } = EdgeNames.Single(pair => pair.Value == bar.Edge).Key;

        [TomlComment("The widgets at the start, in order: each table names one and holds its settings.")]
        public IReadOnlyList<IReadOnlyDictionary<string, object?>> Start
        {
            get;
            set => Set(ref field, value);
        } = Tables(bar.Start);

        [TomlComment("The widgets in the middle.")]
        public IReadOnlyList<IReadOnlyDictionary<string, object?>> Center
        {
            get;
            set => Set(ref field, value);
        } = Tables(bar.Center);

        [TomlComment("The widgets at the end.")]
        public IReadOnlyList<IReadOnlyDictionary<string, object?>> End
        {
            get;
            set => Set(ref field, value);
        } = Tables(bar.End);

        private static IReadOnlyDictionary<string, object?>[] Tables(IReadOnlyList<Placement> placements)
        {
            return
            [
                .. placements.Select(placement =>
                {
                    var table = new Dictionary<string, object?> { ["widget"] = placement.Widget };
                    foreach (var (key, value) in placement.Settings ?? new Dictionary<string, object>())
                        table.TryAdd(key, value);
                    return table;
                })
            ];
        }
    }

    /// <summary>One screen's bar: its areas and their widgets, and its surface and panel once it has them.</summary>
    private sealed class Shown(WidgetArea[] areas) : IDisposable
    {
        public Surface? Surface { get; set; }

        public PanelWindow? Window { get; set; }

        public void Dispose()
        {
            foreach (var area in areas)
                area.Dispose();
            Surface?.Dispose();
            Window?.Dispose();
        }
    }
}
