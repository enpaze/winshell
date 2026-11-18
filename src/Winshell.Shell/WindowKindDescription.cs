namespace Winshell;

internal readonly record struct WindowKindDescription
{
    public bool StartsWithoutFocus { get; init; }

    public bool AlwaysTakesFocus { get; init; }

    public bool TakesFocusWhileArmed { get; init; }

    public bool EscapeDismissesWhileArmed { get; init; }

    public bool GrabsOutsideClicksWhileArmed { get; init; }

    public bool TakesFocusOnShow { get; init; }

    public bool TakesFocusOnShowWhileArmed { get; init; }

    public bool ReplacesSystemFrame { get; init; }

    public bool DismissesMenusOnPress { get; init; }

    public WindowCorners CornersBare { get; init; }

    public WindowCorners CornersWithBackdrop { get; init; }

    public bool HairlineBorderWithBackdrop { get; init; }

    public static WindowKindDescription Of(WindowKind kind) => kind switch
    {
        WindowKind.Panel => Panel,
        WindowKind.Floating => Floating,
        WindowKind.Popup => Popup,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public WindowCorners OwnCorners(WindowBackdrop backdrop) =>
        backdrop == WindowBackdrop.None ? CornersBare : CornersWithBackdrop;

    public bool HairlineBorder(WindowBackdrop backdrop) =>
        HairlineBorderWithBackdrop && backdrop != WindowBackdrop.None;

    private static readonly WindowKindDescription Panel = new()
    {
        StartsWithoutFocus = true,
        TakesFocusWhileArmed = true,
        ReplacesSystemFrame = true,
        DismissesMenusOnPress = true,
        CornersBare = WindowCorners.Square,
        CornersWithBackdrop = WindowCorners.Square
    };

    private static readonly WindowKindDescription Floating = new()
    {
        AlwaysTakesFocus = true,
        TakesFocusOnShow = true,
        CornersBare = WindowCorners.Default,
        CornersWithBackdrop = WindowCorners.Default
    };

    private static readonly WindowKindDescription Popup = new()
    {
        StartsWithoutFocus = true,
        TakesFocusWhileArmed = true,
        EscapeDismissesWhileArmed = true,
        GrabsOutsideClicksWhileArmed = true,
        TakesFocusOnShowWhileArmed = true,
        ReplacesSystemFrame = true,
        DismissesMenusOnPress = true,
        CornersBare = WindowCorners.Square,
        CornersWithBackdrop = WindowCorners.Round,
        HairlineBorderWithBackdrop = true
    };
}
