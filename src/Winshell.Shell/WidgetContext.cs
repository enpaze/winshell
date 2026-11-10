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
/// The props of one placement: the shell, the placement's settings and the area's orientation, plus the styling of a
/// popup the widget opens. A component widget reads them as <c>Props</c>. A changed placement is a new value of this
/// record, and the component renders again. A plain control receives the same value from its factory.
/// </summary>
public sealed record WidgetContext
{
    private readonly PopupStyles _popups;

    internal WidgetContext(Shell shell, WidgetArea area, IReadOnlyDictionary<string, object> settings)
    {
        Shell = shell;
        Orientation = area.Orientation;
        Settings = settings;
        _popups = new PopupStyles(area);
    }

    /// <summary>The shell that owns the area.</summary>
    public Shell Shell { get; }

    /// <summary>The direction in which the area arranges its placements.</summary>
    public Orientation Orientation { get; internal init; }

    /// <summary>This placement's settings. Empty when the placement supplied none.</summary>
    public IReadOnlyDictionary<string, object> Settings { get; internal init; }

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
        ObjectDisposedException.ThrowIf(_popups.Disposed, this);
        return _popups.Wrap(content);
    }

    /// <summary>The same placement with new settings and orientation. Popup styling stays with this placement.</summary>
    internal WidgetContext Next(IReadOnlyDictionary<string, object> settings, Orientation orientation) =>
        this with { Settings = settings, Orientation = orientation };

    internal void StopStyling() => _popups.Stop();

    /// <summary>
    /// Popup wrappers for one placement. A new props record shares this, so a popup opened earlier keeps following the
    /// area after the placement's settings change.
    /// </summary>
    private sealed class PopupStyles(WidgetArea area)
    {
        private readonly List<UserControl> _popups = [];
        private readonly List<(DependencyProperty Property, long Token)> _callbacks = [];

        public bool Disposed { get; private set; }

        public ShellStyle? Style => area.ActiveStyle;

        public FrameworkElement Wrap(UIElement content)
        {
            ArgumentNullException.ThrowIfNull(content);
            var wrapper = new UserControl { Content = content };
            LoadStyle(wrapper);
            CopyStyle(wrapper);
            if (_popups.Count == 0)
            {
                area.ActualThemeChanged += ThemeChanged;
                area.StyleChanged += ReloadStyles;
                foreach (var property in new[] { Control.FontFamilyProperty, Control.FontSizeProperty, Control.ForegroundProperty })
                    _callbacks.Add((property, area.RegisterPropertyChangedCallback(property, (_, _) => CopyStyles())));
            }

            _popups.Add(wrapper);
            return wrapper;
        }

        public void Stop()
        {
            if (Disposed)
                return;
            Disposed = true;
            area.ActualThemeChanged -= ThemeChanged;
            area.StyleChanged -= ReloadStyles;
            foreach (var (property, token) in _callbacks)
                area.UnregisterPropertyChangedCallback(property, token);
            _callbacks.Clear();
            _popups.Clear();
        }

        private void CopyStyle(UserControl wrapper)
        {
            wrapper.RequestedTheme = area.ActualTheme;
            wrapper.FontFamily = area.FontFamily;
            wrapper.FontSize = area.FontSize;
            wrapper.Foreground = area.Foreground;
        }

        private void CopyStyles()
        {
            foreach (var wrapper in _popups)
                CopyStyle(wrapper);
        }

        private void ThemeChanged(FrameworkElement sender, object args) => CopyStyles();

        private void LoadStyle(UserControl wrapper)
        {
            wrapper.Resources.MergedDictionaries.Clear();
            wrapper.Resources.MergedDictionaries.Add(Shell.LoadStyle(area.ActiveStyle));
        }

        private void ReloadStyles()
        {
            foreach (var wrapper in _popups)
                LoadStyle(wrapper);
        }
    }
}

/// <summary>A widget factory throws this with the reason it cannot run in this placement.</summary>
/// <param name="message">The reason shown beside the widget's name in the area's placeholder.</param>
public sealed class UnsupportedWidgetException(string message) : Exception(message);
