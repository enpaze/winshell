using Microsoft.UI.Xaml.Controls;

namespace Winshell;

/// <summary>One entry in a widget area. Several placements may name the same widget.</summary>
/// <param name="Widget">The case-sensitive name registered with <see cref="Widgets.Register"/>.</param>
/// <param name="Settings">This placement's settings, or null for no settings. Widgets should treat nested values as read-only too.</param>
public sealed record Placement(string Widget, IReadOnlyDictionary<string, object>? Settings = null);

/// <summary>The placement's inputs, passed to its widget factory on the shell's thread.</summary>
public sealed class WidgetContext
{
    internal WidgetContext(Shell shell, Orientation orientation, IReadOnlyDictionary<string, object> settings)
    {
        Shell = shell;
        Orientation = orientation;
        Settings = settings;
    }

    /// <summary>The shell that owns the area.</summary>
    public Shell Shell { get; }

    /// <summary>The direction in which the area arranges its placements.</summary>
    public Orientation Orientation { get; }

    /// <summary>This placement's settings. Empty when the placement supplied none.</summary>
    public IReadOnlyDictionary<string, object> Settings { get; }
}

/// <summary>A widget factory throws this with the reason it cannot run in this placement.</summary>
/// <param name="message">The reason shown beside the widget's name in the area's placeholder.</param>
public sealed class UnsupportedWidgetException(string message) : Exception(message);
