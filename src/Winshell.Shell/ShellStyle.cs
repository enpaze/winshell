using System.Globalization;
using System.Security;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Winshell;

public sealed record ShellStyle
{
    public required Color Background { get; init; }

    public required Color Foreground { get; init; }

    public required Color Alert { get; init; }

    public FontFamily FontFamily { get; init; } = new("Segoe UI Variable Text,Segoe UI");

    public double FontSize { get; init; } = 14;

    public double SlotSize { get; init; } = 32;

    public double? Padding { get; init; } = 4;

    internal bool ThemeColors { get; init; }

    internal static ShellStyle BuiltIn { get; } = new()
    {
        Background = default,
        Foreground = default,
        Alert = default,
        ThemeColors = true
    };

    internal string ToXaml()
    {
        var colors = ThemeColors
            ? BuiltInColors
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
        return Page(Themes(colors), metrics);
    }

    private static string Hex(Color color) => string.Create(CultureInfo.InvariantCulture,
        $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);

    private const string Contrast = """
            <ResourceDictionary x:Key="HighContrast">
              <SolidColorBrush x:Key="ShellBackgroundBrush" Color="{ThemeResource SystemColorWindowColor}" />
              <SolidColorBrush x:Key="ShellForegroundBrush" Color="{ThemeResource SystemColorWindowTextColor}" />
              <SolidColorBrush x:Key="ShellAlertBrush" Color="{ThemeResource SystemColorHighlightColor}" />
            </ResourceDictionary>
        """;

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

    private const string BuiltInColors = """
              <SolidColorBrush x:Key="ShellBackgroundBrush" Color="{ThemeResource SolidBackgroundFillColorBase}" />
              <SolidColorBrush x:Key="ShellForegroundBrush" Color="{ThemeResource TextFillColorPrimary}" />
              <SolidColorBrush x:Key="ShellAlertBrush" Color="{ThemeResource SystemFillColorCritical}" />
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

public sealed partial class Shell
{
    private readonly ResourceDictionary _toolTipStyle = [];

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

    public event Action? StyleChanged;

    private void StartToolTipStyle()
    {
        Application.Current.Resources.MergedDictionaries.Add(_toolTipStyle);
        StyleToolTips(LoadStyle(null));
    }

    private void StyleToolTips(ResourceDictionary style)
    {
        _toolTipStyle.Clear();
        if (style.TryGetValue(typeof(ToolTip), out var toolTip))
            _toolTipStyle[typeof(ToolTip)] = toolTip;
    }

    internal static ResourceDictionary LoadStyle(ShellStyle? style) =>
        (ResourceDictionary)XamlReader.Load((style ?? ShellStyle.BuiltIn).ToXaml());
}
