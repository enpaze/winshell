using Winshell.Diagnostics;
using Winshell.Native;

namespace Winshell;

internal sealed partial class HostWindow : IDisposable
{
    private const string ClassName = DoctorScreens.WinshellClass;

    private readonly ShellPlatform _platform;
    private readonly NativeWindow _window;
    private bool _failed;

    public HostWindow(ShellPlatform platform, WindowKind kind, ShellWindow client)
    {
        _platform = platform;
        Kind = kind;
        Client = client;
        Place = new HostPlace(this, platform);
        Placer = client.CreatePlacer(this, platform);
        Presentation = new HostPresentation(this, platform);
        Focus = new WindowFocus(kind, new HostFocusEffects(this, platform));
        Messages = new HostMessages(this, platform);
        var (style, exStyle) = Placer.Styles;
        _window = NativeWindow.Create(ClassName, style, exStyle | WindowFocus.StartingExStyle(kind), OnWindowMessage,
            Fail);
        Handle = _window.Handle;
        try
        {
            Place.Scale = PInvoke.GetDpiForWindow((HWND)Handle) / 96.0;
            Presentation.Attach();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public nint Handle { get; private set; }

    public WindowKind Kind { get; }

    internal ShellWindow Client { get; }

    internal HostPlace Place { get; }

    internal IWindowPlacer Placer { get; }

    internal HostPresentation Presentation { get; }

    internal HostMessages Messages { get; }

    internal WindowFocus Focus { get; }

    internal bool Released { get; private set; }

    private nint? OnWindowMessage(WindowMessage message)
    {
        return _failed ? null : Messages.OnMessage(message);
    }

    private void Fail(Exception error)
    {
        if (_failed)
            return;
        _failed = true;
        _platform.Loop.Post(() =>
        {
            if (Handle != 0)
                Client.OnFailed(error);
        });
    }

    public void Dispose()
    {
        Cleanup(true);
    }

    internal void NoteExternalDestroy()
    {
        if (Released)
            return;
        Cleanup(false);
        _platform.Loop.Post(Client.OnBackingDestroyed);
    }

    private void Cleanup(bool destroyWindow)
    {
        if (Released)
            return;
        Released = true;
        Focus.Release();
        Placer.Release();
        Presentation.Dispose();
        Handle = 0;
        _window.Release(destroyWindow);
        _platform.Forget(this);
    }
}
