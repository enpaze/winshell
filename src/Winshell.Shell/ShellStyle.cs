using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace Winshell;

// The shell's style. Shell's summary is in Shell.cs.
public sealed partial class Shell
{
    private static string? _builtInStyle;

    /// <summary>
    /// The style's <c>ToolTip</c> entry, merged into the app's resources: WinUI opens a tooltip in a popup of its own
    /// and looks for its implicit style only there. Merged, so a tooltip style the configuration puts in the app's
    /// resources wins over it.
    /// </summary>
    private readonly ResourceDictionary _toolTipStyle = [];

    /// <summary>
    /// The source URI of a <c>ResourceDictionary</c> that styles every widget area, styled popup and tooltip, or null
    /// for the built-in style, which follows Windows' colors, Segoe UI and Segoe Fluent Icons. Set it on the shell's
    /// thread. A new value reaches what is already on screen, and the next tooltip that opens. An area's own
    /// <see cref="WidgetArea.Style"/> wins over it, except for tooltips. A file that does not load throws, and the shell
    /// keeps its style.
    /// </summary>
    public Uri? Style
    {
        get;
        set
        {
            var style = LoadStyle(value);
            field = value;
            ShowToolTipStyle(style);
            StyleChanged?.Invoke();
        }
    }

    /// <summary>Raised on the shell's thread after <see cref="Style"/> is set.</summary>
    internal event Action? StyleChanged;

    /// <summary>Starts every tooltip in the shell on the built-in style.</summary>
    private void StartToolTipStyle()
    {
        Application.Current.Resources.MergedDictionaries.Add(_toolTipStyle);
        ShowToolTipStyle(LoadStyle(null));
    }

    /// <summary>Gives tooltips a style's <c>ToolTip</c> entry, or WinUI's look when it has none.</summary>
    private void ShowToolTipStyle(ResourceDictionary style)
    {
        _toolTipStyle.Clear();
        if (style.TryGetValue(typeof(ToolTip), out var toolTip))
            _toolTipStyle[typeof(ToolTip)] = toolTip;
    }

    /// <summary>A fresh dictionary of a style: the one at a URI, or the built-in style for null.</summary>
    internal static ResourceDictionary LoadStyle(Uri? source)
    {
        if (source is not null)
            return new ResourceDictionary { Source = source };
        if (_builtInStyle is null)
        {
            using var stream = typeof(Shell).Assembly.GetManifestResourceStream("Winshell.BuiltInStyle.xaml")!;
            using var reader = new StreamReader(stream);
            _builtInStyle = reader.ReadToEnd();
        }

        return (ResourceDictionary)XamlReader.Load(_builtInStyle);
    }
}
