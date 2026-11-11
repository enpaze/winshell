using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell;

/// <summary>What <see cref="WindowFocus"/> does to the native window, and to the clicks around it.</summary>
internal interface IFocusEffects
{
    /// <summary>Lets the window take activation from the app in front, or stops it.</summary>
    void AllowActivation(bool allow);

    /// <summary>Shows the window, taking activation from the app in front or not.</summary>
    void Show(bool activate);

    /// <summary>Puts the shown window in front.</summary>
    void TakeForeground();

    /// <summary>Puts the window in front and hands its content the keyboard.</summary>
    void TakeKeyboard();

    /// <summary>Starts or stops dismissing the window on a click outside it.</summary>
    void GrabOutsideClicks(bool on);
}

/// <summary>
/// Whether a window takes the keyboard, and the order it does what follows. <see cref="WindowKindDescription"/> says
/// whether it takes focus, whether Esc dismisses it, and when a click outside it does. A panel is armed while it is
/// focusable, and a popup window while it grabs focus.
/// </summary>
internal sealed class WindowFocus(WindowKind kind, IFocusEffects effects)
{
    private readonly WindowKindDescription _kind = WindowKindDescription.Of(kind);

    /// <summary>A panel's focusable setting, or a popup window's grab. The kind says what this changes.</summary>
    private bool _armed;

    private bool _shown;

    /// <summary>The extended style a native window starts with, from the kind's description.</summary>
    internal static uint StartingExStyle(WindowKind kind)
    {
        return WindowKindDescription.Of(kind).StartsWithoutFocus ? (uint)WINDOW_EX_STYLE.WS_EX_NOACTIVATE : 0;
    }

    /// <summary>Whether a click on the window activates it, and with it the keyboard.</summary>
    internal bool ActivatesOnClick => Takes;

    /// <summary>Whether Esc dismisses the window.</summary>
    internal bool DismissesOnEscape => _kind.EscapeDismissesWhileArmed && _armed;

    private bool Takes => _kind.AlwaysTakesFocus || (_kind.TakesFocusWhileArmed && _armed);

    /// <summary>A panel's focusable setting changed.</summary>
    internal void SetPanel(bool focusable) => Arm(focusable);

    /// <summary>A popup window's grab setting changed.</summary>
    internal void SetPopup(bool grabFocus) => Arm(grabFocus);

    private void Arm(bool armed)
    {
        _armed = armed;
        effects.AllowActivation(Takes);
        if (_kind.GrabsOutsideClicksWhileArmed)
            effects.GrabOutsideClicks(_shown && armed);
    }

    /// <summary>Shows the window. A popup window that grabs focus then takes the foreground and starts grabbing clicks.</summary>
    internal void Show()
    {
        _shown = true;
        var armedShow = _kind.TakesFocusOnShowWhileArmed && _armed;
        effects.Show(_kind.TakesFocusOnShow || armedShow);
        if (armedShow)
            effects.TakeForeground();
        if (_kind.GrabsOutsideClicksWhileArmed)
            effects.GrabOutsideClicks(_armed);
    }

    /// <summary>The window is hidden, or going away: a popup window stops grabbing clicks.</summary>
    internal void Release()
    {
        _shown = false;
        if (_kind.GrabsOutsideClicksWhileArmed)
            effects.GrabOutsideClicks(false);
    }

    /// <summary>Takes the keyboard, if this kind of window may.</summary>
    internal void TakeKeyboard()
    {
        if (Takes)
            effects.TakeKeyboard();
    }
}
