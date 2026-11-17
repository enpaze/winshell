namespace Winshell.Input;

internal interface IHotkeyBackend : IDisposable
{
    event Action<OsChord>? ChordPressed;

    HotkeyClaim Register(HotkeyChord chord);

    void Unregister(OsChord chord);

    bool InstallHook(Func<int, bool, bool> onKey, Action onLost);

    void RemoveHook();

    bool IsKeyDown(int virtualKey);

    void SendMask();
}
