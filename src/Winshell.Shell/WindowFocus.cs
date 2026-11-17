using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell;

internal interface IFocusEffects
{
    void AllowActivation(bool allow);

    void Show(bool activate);

    void TakeForeground();

    void TakeKeyboard();

    void GrabOutsideClicks(bool on);
}

internal sealed class WindowFocus(WindowKind kind, IFocusEffects effects)
{
    private readonly WindowKindDescription _kind = WindowKindDescription.Of(kind);

    private bool _armed;

    private bool _shown;

    internal static uint StartingExStyle(WindowKind kind)
    {
        return WindowKindDescription.Of(kind).StartsWithoutFocus ? (uint)WINDOW_EX_STYLE.WS_EX_NOACTIVATE : 0;
    }

    internal bool ActivatesOnClick => Takes;

    internal bool DismissesOnEscape => _kind.EscapeDismissesWhileArmed && _armed;

    private bool Takes => _kind.AlwaysTakesFocus || (_kind.TakesFocusWhileArmed && _armed);

    internal void SetPanel(bool focusable) => Arm(focusable);

    internal void SetPopup(bool grabFocus) => Arm(grabFocus);

    private void Arm(bool armed)
    {
        _armed = armed;
        effects.AllowActivation(Takes);
        if (_kind.GrabsOutsideClicksWhileArmed)
            effects.GrabOutsideClicks(_shown && armed);
    }

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

    internal void Release()
    {
        _shown = false;
        if (_kind.GrabsOutsideClicksWhileArmed)
            effects.GrabOutsideClicks(false);
    }

    internal void TakeKeyboard()
    {
        if (Takes)
            effects.TakeKeyboard();
    }
}
