namespace Winshell.Input;

internal enum HotkeyClaimStatus
{
    Active,
    Taken,
    Invalid
}

internal readonly record struct HotkeyClaim(HotkeyClaimStatus Status, string Error)
{
    public static HotkeyClaim Active { get; } = new(HotkeyClaimStatus.Active, "");

    public static HotkeyClaim Taken(string error)
    {
        return new HotkeyClaim(HotkeyClaimStatus.Taken, error);
    }

    public static HotkeyClaim Invalid(string error)
    {
        return new HotkeyClaim(HotkeyClaimStatus.Invalid, error);
    }
}

internal interface IHotkeyListener
{
    void OnPressed();

    void OnReleased();
}
