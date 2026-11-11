namespace Winshell;

/// <summary>
/// What one <see cref="WindowKind"/> does about focus and its frame. <see cref="WindowFocus"/> and
/// <see cref="HostPresentation"/> both read this. A panel is armed while it is focusable, and a popup window while it
/// grabs focus.
/// </summary>
internal readonly record struct WindowKindDescription
{
    /// <summary>A panel or a popup window starts out not taking focus. A floating window does.</summary>
    public bool StartsWithoutFocus { get; init; }

    /// <summary>A floating window always takes the keyboard.</summary>
    public bool AlwaysTakesFocus { get; init; }

    /// <summary>A panel or a popup window takes the keyboard only while armed.</summary>
    public bool TakesFocusWhileArmed { get; init; }

    /// <summary>Esc dismisses a popup window that is grabbing focus.</summary>
    public bool EscapeDismissesWhileArmed { get; init; }

    /// <summary>A popup window grabs clicks outside it while it is shown and grabbing focus.</summary>
    public bool GrabsOutsideClicksWhileArmed { get; init; }

    /// <summary>Showing a floating window takes focus.</summary>
    public bool TakesFocusOnShow { get; init; }

    /// <summary>
    /// Showing an armed window takes focus and puts it in front. A popup window that grabs focus does. A panel does
    /// not, even while it is focusable.
    /// </summary>
    public bool TakesFocusOnShowWhileArmed { get; init; }

    /// <summary>
    /// A panel or a popup window turns off Windows' fade and peek and sets its own border. A floating window keeps the
    /// frame Windows gives it.
    /// </summary>
    public bool ReplacesSystemFrame { get; init; }

    /// <summary>The kind's own corners with no material behind the content.</summary>
    public WindowCorners CornersBare { get; init; }

    /// <summary>The kind's own corners with a material behind the content. A popup window is rounded; a panel stays square.</summary>
    public WindowCorners CornersWithBackdrop { get; init; }

    /// <summary>A popup window with a material gets Windows' hairline border. Anything else that sets a border gets none.</summary>
    public bool HairlineBorderWithBackdrop { get; init; }

    /// <summary>The one description of <paramref name="kind"/>.</summary>
    public static WindowKindDescription Of(WindowKind kind) => kind switch
    {
        WindowKind.Panel => Panel,
        WindowKind.Floating => Floating,
        WindowKind.Popup => Popup,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    /// <summary>The corners the kind draws when the caller asks for the kind's own. A named corner wins over this.</summary>
    public WindowCorners OwnCorners(WindowBackdrop backdrop) =>
        backdrop == WindowBackdrop.None ? CornersBare : CornersWithBackdrop;

    /// <summary>Whether the border written for a panel or a popup window is Windows' hairline. Otherwise it is none.</summary>
    public bool HairlineBorder(WindowBackdrop backdrop) =>
        HairlineBorderWithBackdrop && backdrop != WindowBackdrop.None;

    private static readonly WindowKindDescription Panel = new()
    {
        StartsWithoutFocus = true,
        TakesFocusWhileArmed = true,
        ReplacesSystemFrame = true,
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
        CornersBare = WindowCorners.Square,
        CornersWithBackdrop = WindowCorners.Round,
        HairlineBorderWithBackdrop = true
    };
}
