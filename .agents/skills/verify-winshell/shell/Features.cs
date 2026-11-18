namespace FeatureMap;

internal static class Features
{
    public static FeatureWindowProps Main { get; set; } = null!;

    public static RowProps Panel { get; set; } = null!;

    public static RowProps Popup { get; set; } = null!;
}

public sealed class PanelRow : Component
{
    public override Element Render() => Component<Row, RowProps>(Features.Panel);
}

public sealed class PopupRow : Component
{
    public override Element Render() => Component<Row, RowProps>(Features.Popup);
}
