using Windows.Foundation;

namespace Winshell;

public sealed record PopupPosition
{
    private readonly Kind _kind;

    private PopupPosition(Kind kind, Point origin = default)
    {
        _kind = kind;
        Origin = origin;
    }

    public static PopupPosition Top { get; } = new(Kind.Top);

    public static PopupPosition Center { get; } = new(Kind.Center);

    public static PopupPosition At(Point origin)
    {
        return new PopupPosition(Kind.At, origin);
    }

    internal bool FollowsActiveWindow => _kind != Kind.At;

    internal Point Origin { get; }

    internal Rect Place(Rect workArea, double width, double height)
    {
        var across = workArea.X + (workArea.Width - width) / 2;
        return _kind switch
        {
            Kind.Top => new Rect(across, workArea.Y + workArea.Height / 5, width, height),
            Kind.Center => new Rect(across, workArea.Y + (workArea.Height - height) / 2, width, height),
            _ => new Rect(Origin.X, Origin.Y, width, height)
        };
    }

    private enum Kind
    {
        Top,
        Center,
        At
    }
}
