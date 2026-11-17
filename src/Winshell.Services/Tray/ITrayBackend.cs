namespace Winshell.Tray;

internal interface ITrayBackend : IDisposable
{
    event Action? TaskbarCreated;

    event Action? ExplorerInFront;

    event Action? SettingsChanged;

    nint Explorer { get; }

    bool TryOwn();

    bool Open(Func<NotifyIconCall, bool> apply);

    void Close();

    void Raise();

    bool Lower();

    void Broadcast();

    IReadOnlyList<TrayIconSetting> ReadSettings();

    (PixelImage? Image, IconResource? Resource) ReadIcon(nint icon);

    PixelImage? DrawIcon(IconResource resource, int size);

    uint ProcessOf(nint window);

    string ProgramOf(nint window);

    uint? IntegrityOf(uint processId);

    (int X, int Y) Cursor { get; }

    void MoveCursor((int X, int Y) point);

    void LetAppTakeForeground(nint window, uint processId);

    void Notify(nint window, uint message, nint wParam, nint lParam);
}
