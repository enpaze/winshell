using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Winshell;

/// <summary>
/// An ordered area of widgets. Use it on the shell's thread and dispose it when its owner closes. Unloading or
/// reparenting does not dispose widgets. The shell's <see cref="Shell.Style"/>, or the area's own, styles its plain
/// controls; area-sensitive XAML uses ThemeResource.
/// Set the inherited Name to make the area a component: its section of the shell's preferences file holds one
/// <c>widgets</c> list, which replaces Defaults while the file has it. An empty name uses only Defaults.
/// </summary>
public sealed partial class WidgetArea : UserControl, IDisposable
{
    private readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal };
    private IReadOnlyList<Placement> _defaults = [];
    private IReadOnlyList<Placement>? _override;
    private readonly Shell _shell;
    private AreaPreferences? _preferences;
    private readonly long _nameChanged;
    private Uri? _style;
    private ResourceDictionary? _styleResources;
    private readonly List<WidgetContext> _contexts = [];
    private bool _disposed;

    internal event Action? StyleChanged;

    /// <summary>Creates an empty, horizontal area in the shell's style.</summary>
    public WidgetArea()
    {
        Content = _panel;
        _shell = Shell.Current;
        LoadStyle(null);
        _shell.StyleChanged += FollowShellStyle;
        _nameChanged = RegisterPropertyChangedCallback(NameProperty, (_, _) => FollowName());
    }

    /// <summary>The layout direction. Changing it disposes and recreates the placements with the new orientation.</summary>
    public Orientation Orientation
    {
        get => _panel.Orientation;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_panel.Orientation == value)
                return;
            _panel.Orientation = value;
            Rebuild();
        }
    }

    /// <summary>
    /// The author's ordered placements. Assigning a list snapshots it, disposes the previous widgets, and creates the
    /// new ones immediately unless the preferences file has a list for this area's Name. To apply edits, assign the
    /// list again. Settings are passed through without copying.
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
                Rebuild();
        }
    }

    /// <summary>
    /// The source URI of this area's own style, which wins over the shell's, or null for <see cref="Shell.Style"/>.
    /// Tooltips follow the shell's style, not the area's. Changing it replaces only the dictionary the style loaded,
    /// leaving the area's other resources intact, and a file that does not load throws and changes nothing. It hides the
    /// inherited WinUI <c>Style</c>, which a widget area does not use.
    /// </summary>
    public new Uri? Style
    {
        get => _style;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            LoadStyle(value);
        }
    }

    /// <summary>The style the area shows: its own, the shell's, or the built-in style for null.</summary>
    internal Uri? ActiveStyle => _style ?? _shell.Style;

    private void FollowShellStyle()
    {
        if (_style is null)
            LoadStyle(null);
    }

    /// <summary>Shows a style, the area's own or null for the shell's. One that does not load changes nothing.</summary>
    private void LoadStyle(Uri? own)
    {
        var next = Shell.LoadStyle(own ?? _shell.Style);
        _style = own;
        if (_styleResources is not null)
            Resources.MergedDictionaries.Remove(_styleResources);
        _styleResources = next;
        // First, so a dictionary the author merged into the area wins over the style.
        Resources.MergedDictionaries.Insert(0, next);
        StyleChanged?.Invoke();
    }

    /// <summary>Reads the section of the area's new name, or none for an empty name.</summary>
    private void FollowName()
    {
        _preferences?.Dispose();
        _preferences = null;
        if (Name.Length > 0)
        {
            var preferences = new AreaPreferences(this);
            preferences.PropertyChanged += (_, _) => ApplyPlacements(preferences.Placements);
            _preferences = preferences;
        }

        ApplyPlacements(_preferences?.Placements);
    }

    private void ApplyPlacements(IReadOnlyList<Placement>? placements)
    {
        if (_disposed)
            return;
        if ((_override is null && placements is null) ||
            (_override is not null && placements is not null && _override.Count == placements.Count &&
             _override.Zip(placements).All(pair => pair.First.Widget == pair.Second.Widget &&
                 TomlDocument.Same(pair.First.Settings, pair.Second.Settings))))
            return;
        _override = placements;
        Rebuild();
    }

    private void Rebuild()
    {
        ClearWidgets();
        foreach (var placement in _override ?? _defaults)
        {
            var context = new WidgetContext(Shell.Current, this,
                placement.Settings ?? ReadOnlyDictionary<string, object>.Empty);
            UIElement widget;
            try
            {
                widget = Widgets.Create(placement.Widget, context);
                _contexts.Add(context);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                context.StopStyling();
                Shell.Current.Log.Error("widgets", $"Could not create {placement.Widget} in area '{Name}': {e}");
                widget = new TextBlock { Text = $"{placement.Widget}: {e.Message}" };
            }

            _panel.Children.Add(widget);
        }
    }

    private void ClearWidgets()
    {
        foreach (var context in _contexts)
            context.StopStyling();
        _contexts.Clear();
        var widgets = _panel.Children.OfType<IDisposable>().ToArray();
        _panel.Children.Clear();
        Shell.DisposeEach(widgets);
    }

    /// <summary>Detaches and disposes every disposable widget, once. All are attempted even if one disposal throws.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _shell.StyleChanged -= FollowShellStyle;
        UnregisterPropertyChangedCallback(NameProperty, _nameChanged);
        _preferences?.Dispose();
        ClearWidgets();
    }

    /// <summary>A named area's section: the <c>widgets</c> list that replaces its defaults while the file has it.</summary>
    private sealed class AreaPreferences(WidgetArea area) : Preferences(area.Name, Check)
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
                foreach (var (name, value) in placement.Settings ?? ReadOnlyDictionary<string, object>.Empty)
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
