using System.Globalization;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell;

/// <summary>
/// What a <see cref="Surface"/> shows. Parent <see cref="Show"/>'s host, and dispose these props when the owner closes.
/// The content is the owner's to dispose: disposing the surface does not dispose it.
/// <code>
/// var area = new WidgetAreaProps { Defaults = ["clock"] };
/// var surface = new SurfaceProps { Content = Component&lt;WidgetArea, WidgetAreaProps&gt;(area) };
/// window.Content = surface.Show();
/// </code>
/// </summary>
public sealed class SurfaceProps : ObservableObject, IDisposable
{
    private readonly Shell _shell = Shell.Current;
    private ReactorHostControl? _host;
    private Element? _content;
    private ShellStyle? _style;
    private Thickness _padding;
    private bool _disposed;

    /// <summary>An empty surface in the shell's style. Make it on the shell's thread.</summary>
    public SurfaceProps()
    {
        _shell.StyleChanged += FollowShellStyle;
    }

    /// <summary>The host <see cref="Show"/> returned, or null before that.</summary>
    public ReactorHostControl? Host => _host;

    /// <summary>
    /// What the surface holds, in its style. The same element stays loaded when the style changes. Null shows nothing.
    /// </summary>
    public Element? Content
    {
        get => _content;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _content = value;
            Changed();
        }
    }

    /// <summary>The space around <see cref="Content"/>, in logical pixels. It does not unload the content.</summary>
    public Thickness Padding
    {
        get => _padding;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_padding == value)
                return;
            _padding = value;
            Changed();
        }
    }

    /// <summary>
    /// The surface's own style, which wins over the shell's, or null for <see cref="Shell.Style"/>. Changing it
    /// replaces the dictionary the background and the content read, and the content stays loaded.
    /// </summary>
    public ShellStyle? Style
    {
        get => _style;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _style = value;
            Restyle();
        }
    }

    /// <summary>Bumps when the dictionary is replaced, so the background reads the new brush. The content is not keyed on it.</summary>
    internal int StyleStamp { get; private set; }

    /// <summary>Mounts this surface. The dictionary is on the host before its tree is, so what it holds inherits the style.</summary>
    public ReactorHostControl Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_host is not null)
            return _host;
        _host = StyleHost.Create<Surface, SurfaceProps>(this);
        StyleHost.Apply(_host, _style);
        return _host;
    }

    /// <summary>Stops following the shell's style and disposes the host. The content is left as it is.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _shell.StyleChanged -= FollowShellStyle;
        _host?.Dispose();
    }

    private void FollowShellStyle()
    {
        if (_style is null)
            Restyle();
    }

    private void Restyle()
    {
        if (_host is not null)
            StyleHost.Apply(_host, _style);
        StyleStamp++;
        Changed(nameof(Style));
    }
}

/// <summary>
/// Paints the style's <c>ShellBackgroundBrush</c> behind its content in the window's theme, as plain controls take
/// theirs, so a switch between light and dark while the shell runs reaches it. A bar's window shows one of these as
/// its tree, with the bar's areas inside it.
/// </summary>
public sealed class Surface : Component<SurfaceProps>
{
    /// <summary>The background under the content. The content is not keyed, so a new style leaves it loaded.</summary>
    public override Element Render()
    {
        var props = UseObservable(Props);
        var content = (props.Content ?? Empty()).Padding(props.Padding);
        // The brush is a {ThemeResource} read where this XAML is parsed, so the style dictionary has to be in that
        // dictionary. A new style parses it again. The content is not keyed, so it stays loaded.
        return Grid(
            [GridSize.Star()],
            [GridSize.Star()],
            XamlHost(() => BackgroundLayer(props.Style ?? Shell.Current.Style))
                .WithKey(string.Create(CultureInfo.InvariantCulture, $"background{props.StyleStamp}"))
                .Grid(0, 0),
            content.Grid(0, 0));
    }

    /// <summary>
    /// A layer whose template paints <c>ShellBackgroundBrush</c>. Parsed with the style merged in, so the theme
    /// resource resolves, and a theme switch reaches it without a new layer.
    /// </summary>
    private static ContentControl BackgroundLayer(ShellStyle? style)
    {
        var xaml = "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                   "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" +
                   "<ResourceDictionary.MergedDictionaries>" + Shell.DictionaryXaml(style) +
                   "</ResourceDictionary.MergedDictionaries>" +
                   "<ControlTemplate x:Key=\"SurfaceBackground\" TargetType=\"ContentControl\">" +
                   "<Border Background=\"{ThemeResource ShellBackgroundBrush}\" />" +
                   "</ControlTemplate></ResourceDictionary>";
        var dictionary = (ResourceDictionary)XamlReader.Load(xaml);
        return new ContentControl
        {
            IsTabStop = false,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Template = (ControlTemplate)dictionary["SurfaceBackground"]
        };
    }
}
