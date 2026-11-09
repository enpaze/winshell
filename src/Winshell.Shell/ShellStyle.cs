using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;

namespace Winshell;

// The shell's style. Shell's summary is in Shell.cs.
public sealed partial class Shell
{
    private static string? _builtInStyle;

    /// <summary>
    /// The source URI of a <c>ResourceDictionary</c> that styles every widget area and styled popup, or null for the
    /// built-in style, which follows Windows' colors, Segoe UI and Segoe Fluent Icons. Set it on the shell's thread.
    /// A new value reaches what is already on screen. An area's own <see cref="WidgetArea.Style"/> wins over it.
    /// </summary>
    public Uri? Style
    {
        get;
        set
        {
            field = value;
            StyleChanged?.Invoke();
        }
    }

    /// <summary>Raised on the shell's thread after <see cref="Style"/> is set.</summary>
    internal event Action? StyleChanged;

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
