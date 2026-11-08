using Winshell;

namespace OmarchyStyle;

/// <summary>
/// Where the bar sits, and which layout each monitor uses. The file is
/// <c>bar/&lt;shell id&gt;.toml</c> under the config root, next to the widget placement file and not part of it.
/// A missing file leaves every property at its default: a top bar and no per-monitor rows.
/// </summary>
internal sealed class BarSettings : TomlFile
{
    public BarSettings(string path, IEventLoop loop, Log log)
        : base(path, loop, log, Check)
    {
    }

    /// <summary>The edge a monitor uses when no row names it: <c>top</c>, <c>bottom</c>, <c>left</c> or <c>right</c>.</summary>
    [TomlComment("top, bottom, left or right. A monitor with no row of its own uses this.")]
    public string Edge
    {
        get;
        set => Set(ref field, value);
    } = "top";

    /// <summary>
    /// One row per monitor that should differ. A row matches manufacturer, model and serial together. A monitor that
    /// is not connected keeps its row and takes no other monitor's bar.
    /// </summary>
    [TomlComment(
        "One monitor that differs from the edge above. Match manufacturer, model and serial together.\n" +
        "edge is optional. layout omitted uses the areas start, center and end; \"side\" uses side.start, side.center and side.end.")]
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Monitor
    {
        get;
        set => Set(ref field, value);
    } = [];

    private static string Check(string key, object value)
    {
        return key == "edge" && !BarLayout.IsEdge(value as string)
            ? "must be \"top\", \"bottom\", \"left\" or \"right\""
            : "";
    }
}

/// <summary>Which edge and layout one monitor's bar uses. Matching never reads the display device name.</summary>
internal static class BarLayout
{
    /// <summary>
    /// The first row whose manufacturer, model and serial all equal this monitor's, or the fallback edge and the
    /// default layout when none do. A row for a monitor that is not connected matches nothing here.
    /// </summary>
    public static BarChoice For(string manufacturer, string model, string serial, string fallbackEdge,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> monitors)
    {
        var fallback = NormalizeEdge(fallbackEdge);
        foreach (var row in monitors)
        {
            if (Text(row, "manufacturer") != manufacturer || Text(row, "model") != model || Text(row, "serial") != serial)
                continue;

            var edge = fallback;
            var layout = Text(row, "layout") ?? "";
            string? warning = null;
            if (Text(row, "edge") is { } written)
            {
                if (IsEdge(written))
                    edge = NormalizeEdge(written);
                else
                    warning = $"edge \"{written}\" must be top, bottom, left or right; using {fallback}.";
            }

            if (layout.Length > 0 && !IsLayout(layout))
            {
                var text = $"layout \"{layout}\" is not a name; using the default areas.";
                warning = warning is null ? text : warning + " " + text;
                layout = "";
            }

            return new BarChoice(edge, layout, warning);
        }

        return new BarChoice(fallback, "", null);
    }

    /// <summary>The placement-file area for one part of a layout. The default layout keeps <paramref name="part"/>.</summary>
    public static string Area(string layout, string part)
    {
        return layout.Length == 0 ? part : layout + "." + part;
    }

    public static bool IsEdge(string? edge)
    {
        return edge?.ToLowerInvariant() is "top" or "bottom" or "left" or "right";
    }

    public static string NormalizeEdge(string? edge)
    {
        return IsEdge(edge) ? edge!.ToLowerInvariant() : "top";
    }

    public static bool Vertical(string edge)
    {
        return edge is "left" or "right";
    }

    public static Edges Anchors(string edge)
    {
        return edge switch
        {
            "bottom" => Edges.Left | Edges.Right | Edges.Bottom,
            "left" => Edges.Left | Edges.Top | Edges.Bottom,
            "right" => Edges.Right | Edges.Top | Edges.Bottom,
            _ => Edges.Left | Edges.Right | Edges.Top
        };
    }

    private static bool IsLayout(string layout)
    {
        if (layout.Length == 0 || (!char.IsAsciiLetter(layout[0]) && layout[0] != '_'))
            return false;
        foreach (var c in layout)
            if (!char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-')
                return false;
        return true;
    }

    /// <summary>A string key, or null when the row omits it or it is not a string. An empty string is present.</summary>
    private static string? Text(IReadOnlyDictionary<string, object?> row, string key)
    {
        return row.TryGetValue(key, out var value) && value is string text ? text : null;
    }
}

/// <param name="Edge">A normalized edge: <c>top</c>, <c>bottom</c>, <c>left</c> or <c>right</c>.</param>
/// <param name="Layout">Empty for the areas <c>start</c>, <c>center</c> and <c>end</c>.</param>
/// <param name="Warning">Why a matched row was partly ignored, or null.</param>
internal readonly record struct BarChoice(string Edge, string Layout, string? Warning);
