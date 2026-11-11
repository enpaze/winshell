using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell;

/// <summary>
/// What a <see cref="WidgetArea"/> shows. Set <see cref="Name"/> to give the area a section of the shell's preferences
/// file. That section holds one <c>widgets</c> list, which replaces <see cref="Defaults"/> while the file has it. An
/// empty name uses only Defaults. The section is created when the name is set, before the area is shown.
/// Parent <see cref="Show"/>'s host, or place <c>Component&lt;WidgetArea, WidgetAreaProps&gt;</c> in a larger tree.
/// Use it on the shell's thread and dispose it when its owner closes. Unloading or reparenting the host does not
/// dispose widgets.
/// </summary>
public sealed class WidgetAreaProps : ObservableObject, IDisposable
{
    private static readonly IReadOnlyDictionary<string, object> EmptySettings = new Dictionary<string, object>();

    private readonly Shell _shell = Shell.Current;
    private readonly PopupAnchor _anchor = new();
    private IReadOnlyList<Placement> _defaults = [];
    private IReadOnlyList<Placement>? _override;
    private AreaPreferences? _preferences;
    private ReactorHostControl? _host;
    private ShellStyle? _style;
    private bool _disposed;

    /// <summary>An empty, horizontal area in the shell's style. Make it on the shell's thread.</summary>
    public WidgetAreaProps()
    {
        _anchor.Style = () => ActiveStyle;
        _shell.StyleChanged += FollowShellStyle;
    }

    /// <summary>The host <see cref="Show"/> returned, or null before that and when the area is only a child of a larger tree.</summary>
    public ReactorHostControl? Host => _host;

    /// <summary>Popups copy this host's theme and font, and the area's style.</summary>
    internal PopupAnchor Anchor => _anchor;

    /// <summary>The style the area shows: its own, the shell's, or null for the built-in style.</summary>
    internal ShellStyle? ActiveStyle => _style ?? _shell.Style;

    /// <summary>True once <see cref="Show"/> put the style dictionary on the host, so the tree does not load a second one.</summary>
    internal bool HostStyled { get; private set; }

    /// <summary>Asks the mounted area to make its widgets again. An edit to a shown type needs a new instance.</summary>
    internal event Action? Remade;

    /// <summary>
    /// The layout direction. Changing it gives each placement the new orientation, and a widget whose name stays
    /// renders again with the new props.
    /// </summary>
    public Orientation Orientation
    {
        get;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (field == value)
                return;
            field = value;
            Changed();
        }
    } = Orientation.Horizontal;

    /// <summary>
    /// The author's ordered placements. Assigning a list snapshots it. A widget whose name stays is rendered again
    /// with the new props. A different list of names disposes the previous widgets and creates the new ones. This
    /// happens immediately unless the preferences file has a list for this area's <see cref="Name"/>. To apply edits,
    /// assign the list again. Settings are passed through without copying.
    /// </summary>
    public IReadOnlyList<Placement> Defaults
    {
        get => _defaults;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(value);
            _defaults = Array.AsReadOnly(value.ToArray());
            if (_override is null)
                Changed();
        }
    }

    /// <summary>
    /// The area's name, which names its section of the preferences file. An empty name uses only
    /// <see cref="Defaults"/>. Changing it reads the section of the new name.
    /// </summary>
    public string Name
    {
        get;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (field == value)
                return;
            field = value ?? throw new ArgumentNullException(nameof(value));
            FollowName();
            Changed();
        }
    } = "";

    /// <summary>
    /// This area's own style, which wins over the shell's, or null for <see cref="Shell.Style"/>. Tooltips follow the
    /// shell's style, not the area's. Changing it replaces only the dictionary the style loaded, leaving the host's
    /// other resources intact.
    /// </summary>
    public ShellStyle? Style
    {
        get => _style;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _style = value;
            if (_host is not null)
                StyleHost.Apply(_host, value);
            _anchor.RaiseStyleChanged();
            Changed();
        }
    }

    /// <summary>The placements on screen: the file's list while it has one, and <see cref="Defaults"/> otherwise.</summary>
    internal Placement[] Current => [.. _override ?? _defaults];

    internal static IReadOnlyDictionary<string, object> SettingsOf(Placement placement) =>
        placement.Settings ?? EmptySettings;

    /// <summary>
    /// Mounts this area as the window's content. The style dictionary is on the host before the tree is, so the
    /// widgets inherit it. Parent the host, and dispose these props when the owner closes.
    /// </summary>
    public ReactorHostControl Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_host is not null)
            return _host;
        _host = StyleHost.Create<WidgetArea, WidgetAreaProps>(this);
        StyleHost.Apply(_host, _style);
        HostStyled = true;
        _anchor.Element = _host;
        return _host;
    }

    internal void Remake()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Remade?.Invoke();
    }

    /// <summary>Stops following the shell and the file, and disposes the host and its widgets, once.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _shell.StyleChanged -= FollowShellStyle;
        _preferences?.Dispose();
        _anchor.Stop();
        _host?.Dispose();
    }

    private void FollowShellStyle()
    {
        if (_disposed || _style is not null)
            return;
        if (_host is not null)
            StyleHost.Apply(_host, null);
        _anchor.RaiseStyleChanged();
        Changed(nameof(Style));
    }

    /// <summary>Reads the section of the area's new name, or none for an empty name.</summary>
    private void FollowName()
    {
        _preferences?.Dispose();
        _preferences = null;
        if (Name.Length > 0)
        {
            var preferences = new AreaPreferences(this);
            preferences.PropertyChanged += (_, _) => Apply(preferences.Placements);
            _preferences = preferences;
        }

        Apply(_preferences?.Placements);
    }

    private void Apply(IReadOnlyList<Placement>? placements)
    {
        if (_disposed)
            return;
        if ((_override is null && placements is null) ||
            (_override is not null && placements is not null && _override.Count == placements.Count &&
             _override.Zip(placements).All(pair => pair.First.Widget == pair.Second.Widget &&
                 TomlDocument.Same(pair.First.Settings, pair.Second.Settings))))
            return;
        _override = placements;
        Changed(nameof(Defaults));
    }

    /// <summary>A named area's section: the <c>widgets</c> list that replaces its defaults while the file has it.</summary>
    private sealed class AreaPreferences(WidgetAreaProps area) : Preferences(area.Name, Check)
    {
        [TomlComment(
            "The widgets, in order: each table names one and holds its settings. The list replaces the shell's whole\n" +
            "default list, and widgets = [] leaves the area empty. Comment it out to use the shell's defaults again.")]
        public IReadOnlyList<IReadOnlyDictionary<string, object?>>? Widgets
        {
            get;
            set => Set(ref field, value);
        }

        /// <summary>The list as placements, or null when the file has none.</summary>
        public IReadOnlyList<Placement>? Placements => Widgets?
            .Select(row => new Placement((string)row["widget"]!,
                row.Where(pair => pair.Key != "widget").ToDictionary(pair => pair.Key, pair => pair.Value!)))
            .ToArray();

        /// <summary>A missing file shows the area's defaults, which are what the list replaces.</summary>
        internal override object? Commented(TomlKey key)
        {
            return area.Defaults.Select(placement =>
            {
                var row = new Dictionary<string, object?> { ["widget"] = placement.Widget };
                foreach (var (name, value) in placement.Settings ?? WidgetAreaProps.EmptySettings)
                    row.TryAdd(name, value);
                return row;
            }).ToArray();
        }

        private static string Check(string key, object value)
        {
            return value is IReadOnlyList<IReadOnlyDictionary<string, object?>> rows && rows.Any(row =>
                row.GetValueOrDefault("widget") is not string widget || string.IsNullOrWhiteSpace(widget))
                ? "must name a widget in each of its tables"
                : "";
        }
    }
}

/// <summary>
/// An ordered area of widgets, in one direction. A bar shows three of these in one tree. The shell's
/// <see cref="Shell.Style"/>, or the area's own, styles the plain controls a widget renders; area-sensitive XAML uses
/// ThemeResource.
/// </summary>
public sealed class WidgetArea : Component<WidgetAreaProps>
{
    /// <summary>The placements, in order. A widget whose name stays is the same component.</summary>
    public override Element Render()
    {
        var props = UseObservable(Props);
        var contexts = UseRef(new List<WidgetContext>());
        var generation = UseRef(0);
        var window = UseRef<ShellWindow?>(null);
        var styled = UseRef(false);
        var pulse = UseRef(0);
        var (_, setPulse) = UseState(0);
        void Bump()
        {
            pulse.Current++;
            setPulse(pulse.Current);
        }

        UseEffect(() =>
        {
            void Remake()
            {
                generation.Current++;
                Bump();
            }

            props.Remade += Remake;
            return () => props.Remade -= Remake;
        }, false);

        var placements = props.Current;
        // A different list of names is a new set of widgets, as a rebuild that keeps the same names renders again.
        var names = UseRef<string[]?>(null);
        var listed = names.Current;
        if (listed is null || listed.Length != placements.Length ||
            !listed.Zip(placements).All(pair => pair.First == pair.Second.Widget))
        {
            names.Current = placements.Select(placement => placement.Widget).ToArray();
            generation.Current++;
        }

        var shown = contexts.Current;
        var slots = new Element[placements.Length];
        for (var i = 0; i < placements.Length; i++)
        {
            var placement = placements[i];
            var settings = WidgetAreaProps.SettingsOf(placement);
            WidgetContext context;
            if (i < shown.Count && shown[i].Widget == placement.Widget)
                context = shown[i] = shown[i].Next(settings, props.Orientation).In(window.Current);
            else
            {
                context = new WidgetContext(Shell.Current, settings, props.Orientation, window.Current, props.Anchor,
                    placement.Widget);
                if (i < shown.Count)
                    shown[i] = context;
                else
                    shown.Add(context);
            }

            slots[i] = Component<Slot, SlotProps>(new SlotProps(context, placement.Widget, props.Name))
                .WithKey($"{i}:{placement.Widget}:{generation.Current}");
        }

        if (shown.Count > placements.Length)
            shown.RemoveRange(placements.Length, shown.Count - placements.Length);

        var stack = props.Orientation == Orientation.Vertical ? VStack(0, slots) : HStack(0, slots);
        return stack.OnMount(element =>
        {
            if (element is not FrameworkElement panel)
                return;
            AreaSessions.Attach(panel, props);
            props.Anchor.Element ??= HostOf(panel);

            void Note()
            {
                var found = ShellWindow.Of(panel);
                if (ReferenceEquals(found, window.Current))
                    return;
                window.Current = found;
                Bump();
            }

            Note();
            panel.Loaded += (_, _) => Note();
            if (props.HostStyled || styled.Current || StyleHost.Matches(panel, props.ActiveStyle))
                return;
            StyleHost.Apply(panel, props.Style);
            styled.Current = true;
            generation.Current++;
            Bump();
        });
    }

    private static Control? HostOf(DependencyObject start)
    {
        for (DependencyObject? node = start; node is not null; node = VisualTreeHelper.GetParent(node))
            if (node is ReactorHostControl host)
                return host;
        return start as Control;
    }

    private sealed record SlotProps(WidgetContext Context, string Widget, string Area);

    /// <summary>One placement. It stops styling popups when the area drops it, and a render error stays in its place.</summary>
    private sealed class Slot : Component<SlotProps>
    {
        public override Element Render()
        {
            var context = Props.Context;
            var widget = Props.Widget;
            var area = Props.Area;
            UseEffect(() => () => context.StopStyling(), false);
            try
            {
                return ErrorBoundary(
                    Widgets.Create(widget, context),
                    error => Component<FailedPlacement, Failure>(new Failure("render", widget, area, error))
                        .WithKey("render"));
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                return Component<FailedPlacement, Failure>(new Failure("create", widget, area, error)).WithKey("create");
            }
        }
    }

    private sealed record Failure(string Verb, string Name, string Area, Exception Error);

    /// <summary>The placeholder and the log line for one placement that could not be shown.</summary>
    private sealed class FailedPlacement : Component<Failure>
    {
        public override Element Render()
        {
            var failure = Props;
            UseEffect(() =>
            {
                Shell.Current.Log.Error("widgets",
                    $"Could not {failure.Verb} {failure.Name} in area '{failure.Area}': {failure.Error}");
                return () => { };
            }, false);
            return TextBlock($"{failure.Name}: {failure.Error.Message}");
        }
    }
}

/// <summary>The area panels on screen, so a hot reload can make one area's widgets again without rebuilding the tree.</summary>
internal static class AreaSessions
{
    private static readonly ConditionalWeakTable<FrameworkElement, WidgetAreaProps> Areas = new();

    public static void Attach(FrameworkElement panel, WidgetAreaProps props)
    {
        if (!Areas.TryGetValue(panel, out _))
            Areas.Add(panel, props);
    }

    public static bool TryGet(DependencyObject element, out WidgetAreaProps props)
    {
        if (element is FrameworkElement panel && Areas.TryGetValue(panel, out props!))
            return true;
        props = null!;
        return false;
    }

    /// <summary>The area stacks under <paramref name="root"/>, in tree order.</summary>
    public static List<StackPanel> Panels(DependencyObject root)
    {
        var found = new List<StackPanel>();
        Walk(root);
        return found;

        void Walk(DependencyObject node)
        {
            if (node is StackPanel panel && Areas.TryGetValue(panel, out _))
                found.Add(panel);
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                Walk(VisualTreeHelper.GetChild(node, i));
        }
    }
}
