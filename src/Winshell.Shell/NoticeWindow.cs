using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;

namespace Winshell;

/// <summary>
/// A notice, drawn like a Windows 11 notification: bottom right of the main screen, above the taskbar, on Acrylic. It does
/// not take the keyboard when it appears. A click on it does, and then Tab moves between its buttons and Escape closes
/// it. Pointing at it, or having the keyboard, holds a timed notice open. <see cref="NoticeComponent"/> draws it.
/// </summary>
internal sealed partial class NoticeWindow : IDisposable
{
    private readonly IEventLoop _loop;
    private readonly TimeSpan _timeout;
    private readonly Action _dismiss;
    private readonly PanelWindow _window;
    private readonly ReactorHostControl _host;
    private IDisposable? _timer;
    private bool _pointer;
    private bool _keyboard;

    public NoticeWindow(Shell shell, Notice notice, Action act, Action dismiss, IEventLoop loop)
    {
        _loop = loop;
        _timeout = notice.Timeout;
        _dismiss = dismiss;
        _window = new PanelWindow(shell)
        {
            Anchors = Edges.Bottom | Edges.Right,
            Margins = new Thickness(0, 0, 12, 12),
            Width = NoticeComponent.Width,
            // Hidden until the component reports its height. It can only measure once it is in the tree.
            Height = 1,
            AboveWindows = true,
            Focusable = true,
            Opacity = 0,
            Backdrop = WindowBackdrop.None,
            Corners = WindowCorners.Round
        };
        _host = new ReactorHostControl
        {
            Props = new NoticeProps(
                notice,
                act,
                dismiss,
                on => Held(out _pointer, on),
                on => Held(out _keyboard, on),
                () => _window.Focus(),
                height =>
                {
                    if (height <= 0)
                        return;
                    _window.Height = height;
                    _window.Backdrop = WindowBackdrop.Acrylic;
                    _window.Opacity = 1;
                }),
            ComponentFactory = () => new NoticeComponent()
        };
        _window.Content = _host;
        _window.Visible = true;
        Hold();
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        _host.Dispose();
        _window.Dispose();
    }

    private void Held(out bool flag, bool on)
    {
        flag = on;
        Hold();
    }

    /// <summary>Runs the time down while no one is at the notice, from the start each time they leave.</summary>
    private void Hold()
    {
        _timer?.Dispose();
        _timer = _timeout > TimeSpan.Zero && !_pointer && !_keyboard ? _loop.Schedule(_timeout, _dismiss) : null;
    }
}
