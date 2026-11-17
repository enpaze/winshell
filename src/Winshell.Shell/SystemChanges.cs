using Winshell.Native;

namespace Winshell;

[Flags]
internal enum SystemChange
{
    None = 0,
    Theme = 1,
    Screens = 2,
    WorkArea = 4
}

internal sealed class SystemChanges
{
    private SystemChange _pending;

    public static SystemChange From(uint message, nint wParam)
    {
        return message switch
        {
            PInvoke.WM_DISPLAYCHANGE => SystemChange.Screens,
            PInvoke.WM_SETTINGCHANGE when wParam == (nint)SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETWORKAREA =>
                SystemChange.WorkArea | SystemChange.Theme,
            PInvoke.WM_SETTINGCHANGE => SystemChange.Screens | SystemChange.Theme,
            PInvoke.WM_DWMCOLORIZATIONCOLORCHANGED or PInvoke.WM_SYSCOLORCHANGE or PInvoke.WM_THEMECHANGED =>
                SystemChange.Theme,
            _ => SystemChange.None
        };
    }

    public bool Add(SystemChange change)
    {
        var first = _pending == SystemChange.None;
        _pending |= change;
        return first && change != SystemChange.None;
    }

    public SystemChange Take()
    {
        var pending = _pending;
        _pending = SystemChange.None;
        return pending;
    }
}
