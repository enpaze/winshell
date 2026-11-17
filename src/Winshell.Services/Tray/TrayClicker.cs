using Winshell.Native;

namespace Winshell.Tray;

internal sealed class TrayClicker(IEventLoop loop, ITrayBackend backend) : IDisposable
{
    private readonly uint? _shellIntegrity = backend.IntegrityOf((uint)Environment.ProcessId);
    private readonly TrayCursor _cursor = new(loop, () => backend.Cursor, backend.MoveCursor);

    public void Dispose()
    {
        _cursor.Dispose();
    }

    public void Click(TrayIcon icon, TrayClick click, (int X, int Y)? at)
    {
        var pid = backend.ProcessOf(icon.Window);
        if (pid == 0 || icon.CallbackMessage == 0)
            return;
        if (!MayClick(_shellIntegrity, backend.IntegrityOf(pid)))
            return;
        var point = at ?? backend.Cursor;
        if (at is { } target)
            _cursor.Place(target);
        backend.LetAppTakeForeground(icon.Window, pid);
        foreach (var message in TrayProtocol.ClickMessages(click, icon.Version))
        {
            var (wParam, lParam) = TrayProtocol.Callback(icon.Version, icon.Id, message, point.X, point.Y);
            backend.Notify(icon.Window, icon.CallbackMessage, wParam, lParam);
        }
    }

    internal static bool MayClick(uint? shell, uint? app)
    {
        return shell <= ProcessIntegrity.Medium || app <= ProcessIntegrity.Medium;
    }
}
