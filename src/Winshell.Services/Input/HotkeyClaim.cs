namespace Winshell.Input;

internal enum HotkeyClaimStatus
{
    Idle,
    Active,
    Taken,
    Invalid
}

internal readonly record struct HotkeyClaim(HotkeyClaimStatus Status, string Error)
{
    public static HotkeyClaim Idle { get; } = new(HotkeyClaimStatus.Idle, "");

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
    IEventLoop Loop { get; }

    void OnPressed(bool deliversRelease);

    void OnReleased();
}
