using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Winshell;

/// <summary>
/// An ordered area of widgets. Use it on the shell's thread and dispose it when its owner closes. Unloading or
/// reparenting does not dispose widgets. Font and foreground inherit normally; area-sensitive XAML uses ThemeResource.
/// </summary>
public sealed partial class WidgetArea : UserControl, IDisposable
{
    private readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal };
    private IReadOnlyList<Placement> _defaults = [];
    private ResourceDictionary? _theme;
    private bool _disposed;

    /// <summary>Creates an empty, horizontal area.</summary>
    public WidgetArea()
    {
        Content = _panel;
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
    /// new ones immediately. To apply edits, assign the list again. Settings are passed through without copying.
    /// </summary>
    public IReadOnlyList<Placement> Defaults
    {
        get => _defaults;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(value);
            _defaults = Array.AsReadOnly(value.ToArray());
            Rebuild();
        }
    }

    /// <summary>
    /// A ResourceDictionary source URI merged into this area's resources, or null for the inherited theme.
    /// Changing it replaces only the dictionary this property loaded, leaving the area's other resources intact.
    /// </summary>
    public Uri? Theme
    {
        get => _theme?.Source;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var next = value is null ? null : new ResourceDictionary { Source = value };
            if (_theme is not null)
                Resources.MergedDictionaries.Remove(_theme);
            _theme = next;
            if (next is not null)
                Resources.MergedDictionaries.Add(next);
        }
    }

    private void Rebuild()
    {
        ClearWidgets();
        foreach (var placement in _defaults)
        {
            var context = new WidgetContext(Shell.Current, Orientation,
                placement.Settings ?? ReadOnlyDictionary<string, object>.Empty);
            UIElement widget;
            try
            {
                widget = Widgets.Create(placement.Widget, context);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                widget = new TextBlock { Text = $"{placement.Widget}: {e.Message}" };
            }

            _panel.Children.Add(widget);
        }
    }

    private void ClearWidgets()
    {
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
        ClearWidgets();
    }
}
