using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Winshell;

/// <summary>
/// One entry in a widget area. Several placements may name the same widget. A string converts to a placement with no
/// settings, so a list of placements can be written <c>["clock", "battery"]</c>.
/// </summary>
/// <param name="Widget">The case-sensitive name registered with <see cref="Widgets.Register"/>.</param>
/// <param name="Settings">This placement's settings, or null for no settings. Widgets should treat nested values as read-only too.</param>
public sealed record Placement(string Widget, IReadOnlyDictionary<string, object>? Settings = null)
{
    /// <summary>A placement of a widget with no settings, so a list of placements can name widgets as plain strings.</summary>
    public static implicit operator Placement(string widget) => new(widget);
}

/// <summary>
/// The props of one placement: the shell, the shell window the area is drawn in, the placement's settings and the
/// area's orientation, plus the styling of a popup the widget opens. A widget reads them as <c>Props</c>. A changed
/// placement, or the area moving into a window, is a new value of this record, and the component renders again.
/// </summary>
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

    /// <summary>The shell that owns the area.</summary>
    public Shell Shell { get; }

    /// <summary>
    /// The shell window this area is drawn in, and so the screen that window is on, or null before the area is in a
    /// window. The area renders the widget again once it is.
    /// </summary>
    public ShellWindow? Window { get; internal init; }

    /// <summary>The direction in which the area arranges its placements.</summary>
    public Orientation Orientation { get; internal init; }

    /// <summary>This placement's settings. Empty when the placement supplied none.</summary>
    public IReadOnlyDictionary<string, object> Settings { get; internal init; }

    /// <summary>The widget this placement shows. A new name is a new component.</summary>
    internal string Widget { get; init; }

    /// <summary>
    /// The style this placement shows: the area's own, or the shell's, or null for the built-in style. A widget reads
    /// <see cref="ShellStyle.Alert"/> from it when something needs attention.
    /// </summary>
    public ShellStyle? Style => _popups.Style;

    /// <summary>
    /// Wraps content for a PopupWindow or a flyout in the area's styling. Assign the result to the popup's Content on
    /// the shell's thread. Each call loads a fresh dictionary of the area's style, preserving the content's own
    /// resources, so the popup's plain controls and a component hosted in it look like the area's. The wrapper follows
    /// the area's resolved theme, style, FontFamily, FontSize and Foreground until the area removes the widget or is
    /// disposed. The widget still owns and disposes its popup windows.
    /// </summary>
    /// <param name="content">The popup's unparented content.</param>
    /// <returns>A new wrapper to use as the popup window's content.</returns>
    public FrameworkElement StylePopup(UIElement content)
    {
        ObjectDisposedException.ThrowIf(_popups.Stopped, this);
        return _popups.Wrap(content);
    }

    /// <summary>The same placement with new settings and orientation. Popup styling and the window stay with it.</summary>
    internal WidgetContext Next(IReadOnlyDictionary<string, object> settings, Orientation orientation) =>
        this with { Settings = settings, Orientation = orientation };

    /// <summary>The same placement drawn in <paramref name="window"/>. Popup styling stays with this placement.</summary>
    internal WidgetContext In(ShellWindow? window) => this with { Window = window };

    internal void StopStyling() => _popups.Stop();

    /// <summary>
    /// Popup wrappers for one placement. A new props record shares this, so a popup opened earlier keeps following the
    /// area after the placement's settings change.
    /// </summary>
    private sealed class PopupStyles
    {
        private readonly PopupAnchor _anchor;
        private readonly List<UserControl> _popups = [];
        private readonly List<(DependencyProperty Property, long Token)> _callbacks = [];
        private bool _following;

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
            var element = _anchor.Element ?? throw new InvalidOperationException("The area is not in a host yet.");
            var wrapper = new UserControl { Content = content };
            LoadStyle(wrapper);
            CopyStyle(wrapper, element);
            if (_popups.Count == 0)
            {
                element.ActualThemeChanged += ThemeChanged;
                _anchor.StyleChanged += ReloadStyles;
                foreach (var property in new[] { Control.FontFamilyProperty, Control.FontSizeProperty, Control.ForegroundProperty })
                    _callbacks.Add((property, element.RegisterPropertyChangedCallback(property, (_, _) => CopyStyles())));
                _following = true;
            }

            _popups.Add(wrapper);
            return wrapper;
        }

        public void Stop()
        {
            if (Stopped)
                return;
            Stopped = true;
            _anchor.Stopping -= Stop;
            _anchor.StyleChanged -= ReloadStyles;
            if (_following && _anchor.Element is { } element)
            {
                element.ActualThemeChanged -= ThemeChanged;
                foreach (var (property, token) in _callbacks)
                    element.UnregisterPropertyChangedCallback(property, token);
            }

            _callbacks.Clear();
            _popups.Clear();
        }

        private void CopyStyle(UserControl wrapper, Control element)
        {
            wrapper.RequestedTheme = element.ActualTheme;
            wrapper.FontFamily = element.FontFamily;
            wrapper.FontSize = element.FontSize;
            wrapper.Foreground = element.Foreground;
        }

        private void CopyStyles()
        {
            if (_anchor.Element is not { } element)
                return;
            foreach (var wrapper in _popups)
                CopyStyle(wrapper, element);
        }

        private void ThemeChanged(FrameworkElement sender, object args) => CopyStyles();

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

/// <summary>
/// Where an area's popups copy their theme, font and style from: the host the area is shown in. One placement's
/// wrappers share it, and disposing the area stops them all.
/// </summary>
internal sealed class PopupAnchor
{
    public Control? Element { get; set; }

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

/// <summary>A widget throws this with the reason it cannot run in this placement.</summary>
/// <param name="message">The reason shown beside the widget's name in the area's placeholder.</param>
public sealed class UnsupportedWidgetException(string message) : Exception(message);
