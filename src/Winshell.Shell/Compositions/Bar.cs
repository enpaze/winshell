using System.Collections.Immutable;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell;

public sealed class Bar : IDisposable
{
    private static readonly Dictionary<string, Edges> EdgeNames = new()
    {
        ["top"] = Edges.Top,
        ["bottom"] = Edges.Bottom,
        ["left"] = Edges.Left,
        ["right"] = Edges.Right
    };

    private readonly Shell _shell = Shell.Current;
    private readonly List<Shown> _open = [];
    private BarPreferences? _preferences;
    private ScreenWindows? _windows;
    private bool _remakePosted;
    private bool _disposed;
    private static ShellStyle? _metricDefaults;

    public Bar()
    {
        _shell.StyleChanged += FollowShellStyle;
        PostRemake();
    }

    public string Name
    {
        get;
        init => field = string.IsNullOrEmpty(value)
            ? throw new ArgumentException("A bar needs a name.", nameof(value))
            : value;
    } = "bar";

    public Edges Edge
    {
        get;
        init => field = EdgeNames.ContainsValue(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "A bar runs along one edge.");
    } = Edges.Top;

    public ImmutableArray<Placement> Start { get; init; } = [];

    public ImmutableArray<Placement> Center { get; init; } = [];

    public ImmutableArray<Placement> End { get; init; } = [];

    public bool Reserve { get; init; } = true;

    public double? Thickness { get; init; }

    public double? Padding { get; init; }

    public ShellStyle? Style { get; init; }

    private void FollowShellStyle()
    {
        if (Style is not null)
            return;
        var thickness = SlotSize(Thickness, null, _shell.Style);
        foreach (var shown in _open)
            shown.Resize(thickness, Reserve);
    }

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

    private IDisposable Show(ScreenInfo screen, IReadOnlyDictionary<string, object?>? row)
    {
        var section = _preferences!;
        var edge = EdgeNames[(string)Pick("edge", section.Edge)];
        var vertical = edge is Edges.Left or Edges.Right;
        ReactorHostControl? host = null;
        try
        {
            var thickness = SlotSize(Thickness, Style, _shell.Style);
            var areas = Grid(
                [GridSize.Star()],
                [GridSize.Star()],
                Placed("start", section.Start, HorizontalAlignment.Left, VerticalAlignment.Top),
                Placed("center", section.Center, HorizontalAlignment.Center, VerticalAlignment.Center),
                Placed("end", section.End, HorizontalAlignment.Right, VerticalAlignment.Bottom));
            var content = Memo(ctx =>
            {
                var (_, bump) = ctx.UseReducer(0);
                var follows = Style is null && Padding is null;
                ctx.UseEffect(() =>
                {
                    if (!follows)
                        return () => { };
                    void Follow() => bump(_ => _ + 1);
                    var shell = Shell.Current;
                    shell.StyleChanged += Follow;
                    return () => shell.StyleChanged -= Follow;
                }, follows);
                var padding = EndPadding(Padding, Style, Shell.Current.Style);
                var gap = vertical ? new Thickness(0, padding, 0, padding) : new Thickness(padding, 0, padding, 0);
                return areas.Padding(gap);
            }, Style, Padding, vertical);
            host = new ReactorHostControl
            {
                ComponentFactory = () => new Surface(),
                Props = new SurfaceProps(content, default, Style),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                IsTabStop = false
            };
            var window = new PanelWindow
            {
                Screen = screen,
                Anchors = edge | (vertical ? Edges.Top | Edges.Bottom : Edges.Left | Edges.Right),
                ReservedSize = Reserve ? thickness : 0,
                Content = host
            };
            var shown = new Shown(host, window, vertical, _open);
            shown.Resize(thickness, Reserve);
            window.Visible = true;
            _open.Add(shown);
            return shown;
        }
        catch
        {
            host?.Dispose();
            throw;
        }

        Element Placed(string key, object placements, HorizontalAlignment across, VerticalAlignment down) =>
            Elements.WidgetArea(defaults: Placement.Parse(Pick(key, placements)),
                    orientation: vertical ? Orientation.Vertical : Orientation.Horizontal, style: Style)
                .HAlign(vertical ? HorizontalAlignment.Stretch : across)
                .VAlign(vertical ? down : VerticalAlignment.Stretch)
                .WithKey($"{across}:{down}")
                .Grid(0, 0);

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

    private static string Check(string key, object value)
    {
        if (key == "edge")
            return value is string edge && EdgeNames.ContainsKey(edge)
                ? ""
                : "must be \"top\", \"bottom\", \"left\" or \"right\"";
        return Placement.Check(value).Length == 0 ? "" : "must be a list of tables that each name a widget";
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _shell.StyleChanged -= FollowShellStyle;
        _windows?.Dispose();
        _preferences?.Dispose();
    }

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
        } = Placement.Format(bar.Start);

        [TomlComment("The widgets in the middle.")]
        public IReadOnlyList<IReadOnlyDictionary<string, object?>> Center
        {
            get;
            set => Set(ref field, value);
        } = Placement.Format(bar.Center);

        [TomlComment("The widgets at the end.")]
        public IReadOnlyList<IReadOnlyDictionary<string, object?>> End
        {
            get;
            set => Set(ref field, value);
        } = Placement.Format(bar.End);
    }

    private static double SlotSize(double? thickness, ShellStyle? own, ShellStyle? shell)
    {
        if (thickness is { } set)
            return set;
        return (own ?? shell ?? MetricDefaults()).SlotSize;
    }

    private static double EndPadding(double? padding, ShellStyle? own, ShellStyle? shell)
    {
        if (padding is { } set)
            return set;
        var look = own ?? shell;
        return look is null ? MetricDefaults().Padding ?? 0 : look.Padding ?? 0;
    }

    private static ShellStyle MetricDefaults() => _metricDefaults ??= new()
    {
        Background = default,
        Foreground = default,
        Alert = default
    };

    private sealed class Shown(ReactorHostControl host, PanelWindow window, bool vertical, List<Shown> open) : IDisposable
    {
        public void Resize(double thickness, bool reserve)
        {
            window.ReservedSize = reserve ? thickness : 0;
            if (vertical)
                window.Width = thickness;
            else
                window.Height = thickness;
        }

        public void Dispose()
        {
            open.Remove(this);
            host.Dispose();
            window.Dispose();
        }
    }
}
