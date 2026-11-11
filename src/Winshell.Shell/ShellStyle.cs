using System.Globalization;
using System.Security;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Winshell;

/// <summary>
/// The look of a shell, a bar or a widget area: background, foreground, alert color, font, slot size and padding.
/// Winshell writes it into the resources plain controls read. Null, on the shell, is the built-in style, which follows
/// Windows' colors in light, in dark and in a contrast theme.
/// </summary>
public sealed record ShellStyle
{
    /// <summary>Behind a surface, such as a bar, and a popup.</summary>
    public required Color Background { get; init; }

    /// <summary>Text, and a glyph that takes its color from the control that holds it.</summary>
    public required Color Foreground { get; init; }

    /// <summary>
    /// Something that needs attention, such as a network with no internet. A widget reads it from
    /// <see cref="WidgetContext.Style"/>.
    /// </summary>
    public required Color Alert { get; init; }

    /// <summary>Text. The built-in style uses Segoe UI Variable, then Segoe UI.</summary>
    public FontFamily FontFamily { get; init; } = new("Segoe UI Variable Text,Segoe UI");

    /// <summary>Text, in logical pixels. The built-in style uses 14.</summary>
    public double FontSize { get; init; } = 14;

    /// <summary>One button, and the thickness of a bar, in logical pixels. The built-in style uses 32.</summary>
    public double SlotSize { get; init; } = 32;

    /// <summary>The space at each end of a bar, in logical pixels. Null gives a bar none. The built-in style uses 4.</summary>
    public double? Padding { get; init; } = 4;

    /// <summary>
    /// The built-in style paints with Windows' theme brushes. A custom style paints with <see cref="Background"/>,
    /// <see cref="Foreground"/> and <see cref="Alert"/>.
    /// </summary>
    internal bool ThemeColors { get; init; }

    /// <summary>The built-in style. <see cref="Shell.Style"/> stays null for it.</summary>
    internal static ShellStyle BuiltIn { get; } = new()
    {
        Background = default,
        Foreground = default,
        Alert = default,
        ThemeColors = true
    };

    /// <summary>The resource dictionary a widget area, surface and styled popup merge.</summary>
    internal string ToXaml()
    {
        var colors = ThemeColors
            ? ""
            : string.Create(CultureInfo.InvariantCulture,
                $"""
                       <SolidColorBrush x:Key="ShellBackgroundBrush" Color="{Hex(Background)}" />
                       <SolidColorBrush x:Key="ShellForegroundBrush" Color="{Hex(Foreground)}" />
                       <SolidColorBrush x:Key="ShellAlertBrush" Color="{Hex(Alert)}" />
                 """);
        var padding = Padding is { } space
            ? string.Create(CultureInfo.InvariantCulture, $"""<x:Double x:Key="ShellPadding">{Number(space)}</x:Double>""")
            : "";
        var metrics = string.Create(CultureInfo.InvariantCulture,
            $"""
                   <FontFamily x:Key="ShellFontFamily">{SecurityElement.Escape(FontFamily.Source)}</FontFamily>
                   <x:Double x:Key="ShellFontSize">{Number(FontSize)}</x:Double>
                   <x:Double x:Key="ShellSlotSize">{Number(SlotSize)}</x:Double>
                   {padding}
             """);
        return Page(ThemeColors ? BuiltInBrushes + Contrast : Themes(colors), metrics);
    }

    private static string Hex(Color color) => string.Create(CultureInfo.InvariantCulture,
        $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);

    // The colors a contrast theme uses, in place of the style's own, so the shell stays readable.
    private const string Contrast = """
            <ResourceDictionary x:Key="HighContrast">
              <SolidColorBrush x:Key="ShellBackgroundBrush" Color="{ThemeResource SystemColorWindowColor}" />
              <SolidColorBrush x:Key="ShellForegroundBrush" Color="{ThemeResource SystemColorWindowTextColor}" />
              <SolidColorBrush x:Key="ShellAlertBrush" Color="{ThemeResource SystemColorHighlightColor}" />
            </ResourceDictionary>
        """;

    // The same implicit styles for every style, so a button, a line, an icon and a tooltip match the shell.
    private const string Controls = """
          <Style TargetType="Button">
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="BorderThickness" Value="0" />
            <Setter Property="Padding" Value="0" />
            <Setter Property="MinWidth" Value="{ThemeResource ShellSlotSize}" />
            <Setter Property="MinHeight" Value="{ThemeResource ShellSlotSize}" />
            <Setter Property="HorizontalContentAlignment" Value="Center" />
            <Setter Property="VerticalContentAlignment" Value="Center" />
            <Setter Property="VerticalAlignment" Value="Center" />
            <Setter Property="Foreground" Value="{ThemeResource ShellForegroundBrush}" />
            <Setter Property="FontFamily" Value="{ThemeResource ShellFontFamily}" />
            <Setter Property="FontSize" Value="{ThemeResource ShellFontSize}" />
          </Style>
          <Style TargetType="TextBlock">
            <Setter Property="Foreground" Value="{ThemeResource ShellForegroundBrush}" />
            <Setter Property="FontFamily" Value="{ThemeResource ShellFontFamily}" />
            <Setter Property="FontSize" Value="{ThemeResource ShellFontSize}" />
            <Setter Property="VerticalAlignment" Value="Center" />
          </Style>
          <Style TargetType="FontIcon">
            <Setter Property="FontFamily" Value="Segoe Fluent Icons,Segoe MDL2 Assets" />
            <Setter Property="FontSize" Value="16" />
          </Style>
          <Style TargetType="ToolTip">
            <Setter Property="Background" Value="{ThemeResource ShellBackgroundBrush}" />
            <Setter Property="Foreground" Value="{ThemeResource ShellForegroundBrush}" />
            <Setter Property="FontFamily" Value="{ThemeResource ShellFontFamily}" />
          </Style>
        """;

    private const string BuiltInBrushes = """
            <ResourceDictionary x:Key="Default">
              <SolidColorBrush x:Key="ShellBackgroundBrush" Color="{ThemeResource SolidBackgroundFillColorBase}" />
              <SolidColorBrush x:Key="ShellForegroundBrush" Color="{ThemeResource TextFillColorPrimary}" />
              <SolidColorBrush x:Key="ShellAlertBrush" Color="{ThemeResource SystemFillColorCritical}" />
            </ResourceDictionary>
            <ResourceDictionary x:Key="Light">
              <SolidColorBrush x:Key="ShellBackgroundBrush" Color="{ThemeResource SolidBackgroundFillColorBase}" />
              <SolidColorBrush x:Key="ShellForegroundBrush" Color="{ThemeResource TextFillColorPrimary}" />
              <SolidColorBrush x:Key="ShellAlertBrush" Color="{ThemeResource SystemFillColorCritical}" />
            </ResourceDictionary>
        """;

    private static string Themes(string colors) =>
        $"""
            <ResourceDictionary x:Key="Default">
            {colors}
            </ResourceDictionary>
            <ResourceDictionary x:Key="Light">
            {colors}
            </ResourceDictionary>
        """ + Contrast;

    private static string Page(string themes, string metrics) =>
        $"""
        <ResourceDictionary
          xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <ResourceDictionary.ThemeDictionaries>
        {themes}
          </ResourceDictionary.ThemeDictionaries>
        {metrics}
        {Controls}
        </ResourceDictionary>
        """;
}

// The shell's style. Shell's summary is in Shell.cs.
public sealed partial class Shell
{
    /// <summary>
    /// The style's <c>ToolTip</c> entry, merged into the app's resources: WinUI opens a tooltip in a popup of its own
    /// and looks for its implicit style only there. Merged, so a tooltip style the configuration puts in the app's
    /// resources wins over it.
    /// </summary>
    private readonly ResourceDictionary _toolTipStyle = [];

    /// <summary>
    /// The style of every widget area, <see cref="Surface"/>, styled popup and tooltip, or null for the built-in
    /// style, which follows Windows' colors, Segoe UI and Segoe Fluent Icons. Set it on the shell's thread. A new
    /// value reaches what is already on screen, and the next tooltip that opens. A bar's, an area's or a surface's own
    /// style wins over it, except for tooltips.
    /// </summary>
    public ShellStyle? Style
    {
        get;
        set
        {
            var style = LoadStyle(value);
            field = value;
            StyleToolTips(style);
            StyleChanged?.Invoke();
        }
    }

    /// <summary>
    /// Raised on the shell's thread after <see cref="Style"/> is set, so what reads the style outside a widget area, such
    /// as the thickness of a <see cref="Bar"/>, can read it again.
    /// </summary>
    public event Action? StyleChanged;

    /// <summary>Starts every tooltip in the shell on the built-in style.</summary>
    private void StartToolTipStyle()
    {
        Application.Current.Resources.MergedDictionaries.Add(_toolTipStyle);
        StyleToolTips(LoadStyle(null));
    }

    /// <summary>Gives tooltips a style's <c>ToolTip</c> entry, or WinUI's look when it has none.</summary>
    private void StyleToolTips(ResourceDictionary style)
    {
        _toolTipStyle.Clear();
        if (style.TryGetValue(typeof(ToolTip), out var toolTip))
            _toolTipStyle[typeof(ToolTip)] = toolTip;
    }

    /// <summary>A fresh dictionary of a style, or of the built-in style for null.</summary>
    internal static ResourceDictionary LoadStyle(ShellStyle? style) =>
        (ResourceDictionary)XamlReader.Load(DictionaryXaml(style));

    /// <summary>The XAML of one resolved style. Null is the built-in style.</summary>
    internal static string DictionaryXaml(ShellStyle? style) => (style ?? ShellStyle.BuiltIn).ToXaml();
}
