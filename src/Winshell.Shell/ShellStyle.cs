using System.Globalization;
using System.Security;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Windows.UI;

namespace Winshell;

public sealed record ShellStyle
{
    public Color? Background { get; init; }

    public Color? Foreground { get; init; }

    public Color? Alert { get; init; }

    public string FontFamily { get; init; } = "Segoe UI Variable Text,Segoe UI";

    public double FontSize { get; init; } = 14;

    public double SlotSize { get; init; } = 32;

    public double? Padding { get; init; } = 4;

    internal string ToXaml(bool alert = false)
    {
        var colors = string.Create(CultureInfo.InvariantCulture,
            $"""
                   <SolidColorBrush x:Key="ShellBackgroundBrush" Color="{Value(Background, "SolidBackgroundFillColorBase")}" />
                   <SolidColorBrush x:Key="ShellForegroundBrush" Color="{Value(Foreground, "TextFillColorPrimary")}" />
                   <SolidColorBrush x:Key="ShellAlertBrush" Color="{Value(Alert, "SystemFillColorCritical")}" />
             """);
        var padding = Padding is { } space
            ? string.Create(CultureInfo.InvariantCulture, $"""<x:Double x:Key="ShellPadding">{Number(space)}</x:Double>""")
            : "";
        var metrics = string.Create(CultureInfo.InvariantCulture,
            $"""
                   <FontFamily x:Key="ShellFontFamily">{SecurityElement.Escape(FontFamily)}</FontFamily>
                   <x:Double x:Key="ShellFontSize">{Number(FontSize)}</x:Double>
                   <x:Double x:Key="ShellSlotSize">{Number(SlotSize)}</x:Double>
                   {padding}
             """);
        return Page(Themes(colors), metrics, Controls(alert));
    }

    private static string Value(Color? color, string windows) => color is { } set
        ? string.Create(CultureInfo.InvariantCulture, $"#{set.A:X2}{set.R:X2}{set.G:X2}{set.B:X2}")
        : $"{{ThemeResource {windows}}}";

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);

    private const string Contrast = """
            <ResourceDictionary x:Key="HighContrast">
              <SolidColorBrush x:Key="ShellBackgroundBrush" Color="{ThemeResource SystemColorWindowColor}" />
              <SolidColorBrush x:Key="ShellForegroundBrush" Color="{ThemeResource SystemColorWindowTextColor}" />
              <SolidColorBrush x:Key="ShellAlertBrush" Color="{ThemeResource SystemColorHighlightColor}" />
            </ResourceDictionary>
        """;

    private static string Controls(bool alert)
    {
        var foreground = alert ? "ShellAlertBrush" : "ShellForegroundBrush";
        var icon = alert ? $$"""<Setter Property="Foreground" Value="{ThemeResource {{foreground}}}" />""" : "";
        return $$"""
          <Style TargetType="Button">
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="BorderThickness" Value="0" />
            <Setter Property="Padding" Value="0" />
            <Setter Property="MinWidth" Value="{ThemeResource ShellSlotSize}" />
            <Setter Property="MinHeight" Value="{ThemeResource ShellSlotSize}" />
            <Setter Property="HorizontalContentAlignment" Value="Center" />
            <Setter Property="VerticalContentAlignment" Value="Center" />
            <Setter Property="VerticalAlignment" Value="Center" />
            <Setter Property="Foreground" Value="{ThemeResource {{foreground}}}" />
            <Setter Property="FontFamily" Value="{ThemeResource ShellFontFamily}" />
            <Setter Property="FontSize" Value="{ThemeResource ShellFontSize}" />
          </Style>
          <Style TargetType="TextBlock">
            <Setter Property="Foreground" Value="{ThemeResource {{foreground}}}" />
            <Setter Property="FontFamily" Value="{ThemeResource ShellFontFamily}" />
            <Setter Property="FontSize" Value="{ThemeResource ShellFontSize}" />
            <Setter Property="VerticalAlignment" Value="Center" />
          </Style>
          <Style TargetType="FontIcon">
            <Setter Property="FontFamily" Value="Segoe Fluent Icons,Segoe MDL2 Assets" />
            <Setter Property="FontSize" Value="16" />
            {{icon}}
          </Style>
          <Style TargetType="ToolTip">
            <Setter Property="Background" Value="{ThemeResource ShellBackgroundBrush}" />
            <Setter Property="Foreground" Value="{ThemeResource ShellForegroundBrush}" />
            <Setter Property="FontFamily" Value="{ThemeResource ShellFontFamily}" />
          </Style>
        """;
    }

    private static string Themes(string colors) =>
        $"""
            <ResourceDictionary x:Key="Default">
            {colors}
            </ResourceDictionary>
            <ResourceDictionary x:Key="Light">
            {colors}
            </ResourceDictionary>
        """ + Contrast;

    private static string Page(string themes, string metrics, string controls) =>
        $"""
        <ResourceDictionary
          xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <ResourceDictionary.ThemeDictionaries>
        {themes}
          </ResourceDictionary.ThemeDictionaries>
        {metrics}
        {controls}
        </ResourceDictionary>
        """;
}

public sealed partial class Shell
{
    private readonly ResourceDictionary _toolTipStyle = [];

    public ShellStyle Style
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var style = LoadStyle(value);
            field = value;
            StyleToolTips(style);
            StyleChanged?.Invoke();
        }
    } = new();

    public event Action? StyleChanged;

    private void StartToolTipStyle()
    {
        Application.Current.Resources.MergedDictionaries.Add(_toolTipStyle);
        StyleToolTips(LoadStyle(Style));
    }

    private void StyleToolTips(ResourceDictionary style)
    {
        _toolTipStyle.Clear();
        if (style.TryGetValue(typeof(ToolTip), out var toolTip))
            _toolTipStyle[typeof(ToolTip)] = toolTip;
    }

    internal static ResourceDictionary LoadStyle(ShellStyle style, bool alert = false) =>
        (ResourceDictionary)XamlReader.Load(style.ToXaml(alert));
}
