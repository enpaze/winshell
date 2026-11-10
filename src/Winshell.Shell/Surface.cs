using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace Winshell;

/// <summary>
/// A control that paints the style's <c>ShellBackgroundBrush</c> behind its content in the window's theme, as plain
/// controls take theirs, so a switch between light and dark while the shell runs reaches it. What it holds is in its
/// style too. Make it the root of a bar, a dock or any composition, use it on the shell's thread, and dispose it when
/// its owner closes. The style gives its background, so its own <c>Background</c>, border and corners don't show.
/// <code>
/// var surface = new Surface { Content = new WidgetArea { Defaults = ["clock"] } };
/// </code>
/// </summary>
public sealed partial class Surface : ContentControl, IDisposable
{
    // The background is a layer of its own under the content, so a new style replaces only the layer, and what the
    // surface holds stays loaded.
    private const string Layers = """
        <ControlTemplate
          xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          TargetType="ContentControl">
          <Grid>
            <ContentControl x:Name="BackgroundLayer" IsTabStop="False" />
            <ContentPresenter
              Margin="{TemplateBinding Padding}"
              HorizontalContentAlignment="{TemplateBinding HorizontalContentAlignment}"
              VerticalContentAlignment="{TemplateBinding VerticalContentAlignment}" />
          </Grid>
        </ControlTemplate>
        """;

    // WinUI reads a brush from code in the app's theme, not the window's, and looks a {ThemeResource} up in the
    // dictionaries in scope where the XAML is read, not in the control's resources. So the layer's template is read in
    // a dictionary that merges the style, and a style needs only its ShellBackgroundBrush.
    private const string Styled = """
        <ResourceDictionary
          xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <ResourceDictionary.MergedDictionaries>
            {0}
          </ResourceDictionary.MergedDictionaries>
          <ControlTemplate x:Key="SurfaceBackground" TargetType="ContentControl">
            <Border Background="{ThemeResource ShellBackgroundBrush}" />
          </ControlTemplate>
        </ResourceDictionary>
        """;

    private readonly Shell _shell = Shell.Current;
    private ShellStyle? _style;
    private ResourceDictionary? _styleResources;
    private ContentControl? _background;
    private bool _disposed;

    /// <summary>An empty surface in the shell's style.</summary>
    public Surface()
    {
        IsTabStop = false;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Template = (ControlTemplate)XamlReader.Load(Layers);
        LoadStyle(null);
        _shell.StyleChanged += FollowShellStyle;
    }

    /// <summary>
    /// The surface's own style, which wins over the shell's, or null for <see cref="Shell.Style"/>. It hides the
    /// inherited WinUI <c>Style</c>, which a surface does not use.
    /// </summary>
    public new ShellStyle? Style
    {
        get => _style;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            LoadStyle(value);
        }
    }

    /// <summary>Gives the background layer the style's template once the surface's own is applied.</summary>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _background = (ContentControl)GetTemplateChild("BackgroundLayer");
        _background.Template = (ControlTemplate)_styleResources!["SurfaceBackground"];
    }

    private void FollowShellStyle()
    {
        if (_style is null)
            LoadStyle(null);
    }

    /// <summary>Shows the surface's own style, or the shell's for null.</summary>
    private void LoadStyle(ShellStyle? own)
    {
        var next = (ResourceDictionary)XamlReader.Load(
            Styled.Replace("{0}", Shell.DictionaryXaml(own ?? _shell.Style), StringComparison.Ordinal));
        _style = own;
        if (_styleResources is not null)
            Resources.MergedDictionaries.Remove(_styleResources);
        _styleResources = next;
        // First, so a dictionary the author merged into the surface wins over the style for what the surface holds.
        // The background is always the style's.
        Resources.MergedDictionaries.Insert(0, next);
        // A new template reads the background again, from the new style.
        _background?.Template = (ControlTemplate)next["SurfaceBackground"];
    }

    /// <summary>Stops following the shell's style. The content is the owner's to dispose.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _shell.StyleChanged -= FollowShellStyle;
    }
}
