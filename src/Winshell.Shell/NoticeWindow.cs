using Microsoft.UI;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Winshell;

internal sealed partial class NoticeWindow : IDisposable
{
    private readonly IEventLoop _loop;
    private readonly TimeSpan _timeout;
    private readonly Action _dismiss;
    private readonly PanelWindow _window;
    private readonly ReactorHostControl _host;
    private FrameworkElement? _notice;
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
            Edge = Edge.Bottom,
            Align = PanelAlign.End,
            Margins = new Thickness(0, 0, 12, 12),
            Width = NoticeComponent.Width,
            Height = 1,
            AboveWindows = true,
            Focusable = true,
            Opacity = 0,
            Backdrop = WindowBackdrop.None,
            Corners = WindowCorners.Round
        };
        _host = new ReactorHostControl
        {
            Background = new SolidColorBrush(Colors.Transparent),
            Props = new NoticeProps(notice, act, dismiss),
            ComponentFactory = () => new NoticeComponent()
        };
        _host.PointerEntered += (_, _) => Held(ref _pointer, true);
        _host.PointerExited += (_, _) => Held(ref _pointer, false);
        _host.GotFocus += (_, _) => Held(ref _keyboard, true);
        _host.LostFocus += (_, _) => Held(ref _keyboard, false);
        _host.PointerPressed += OnPointerPressed;
        _host.Loaded += OnHostLoaded;
        _window.Content = _host;
        _window.Start();
        Hold();
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        _host.Loaded -= OnHostLoaded;
        _host.LayoutUpdated -= OnLaidOut;
        _host.PointerPressed -= OnPointerPressed;
        if (_notice is not null)
            _notice.Loaded -= OnContentLoaded;
        _host.Dispose();
        _window.Dispose();
    }

    private void OnHostLoaded(object sender, RoutedEventArgs e)
    {
        _host.Loaded -= OnHostLoaded;
        _host.LayoutUpdated += OnLaidOut;
    }

    private void OnLaidOut(object? sender, object e)
    {
        if (_host.Content is not FrameworkElement content)
            return;
        if (!ReferenceEquals(_notice, content))
        {
            if (_notice is not null)
                _notice.Loaded -= OnContentLoaded;
            _notice = content;
            content.Loaded += OnContentLoaded;
        }

        SizeFrom(content);
    }

    private void OnContentLoaded(object sender, RoutedEventArgs e)
    {
        var content = (FrameworkElement)sender;
        content.Loaded -= OnContentLoaded;
        content.UpdateLayout();
        SizeFrom(content);
    }

    private void SizeFrom(FrameworkElement content)
    {
        if (!content.IsLoaded)
            return;
        var height = Math.Ceiling(Math.Max(content.ActualHeight, content.DesiredSize.Height));
        if (height <= _window.Height + 1)
            return;
        _host.LayoutUpdated -= OnLaidOut;
        _window.Height = height;
        _window.Backdrop = WindowBackdrop.Acrylic;
        _window.Opacity = 1;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _window.Focus();
        if (FocusManager.FindFirstFocusableElement(_host) is Control first)
            first.Focus(FocusState.Programmatic);
    }

    private void Held(ref bool flag, bool on)
    {
        flag = on;
        Hold();
    }

    private void Hold()
    {
        _timer?.Dispose();
        _timer = _timeout > TimeSpan.Zero && !_pointer && !_keyboard ? _loop.Schedule(_timeout, _dismiss) : null;
    }
}
