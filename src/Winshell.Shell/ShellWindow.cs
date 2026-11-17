using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace Winshell;

public abstract class ShellWindow : ObservableObject, IDisposable
{
    private readonly Shell _shell;
    private HostWindow? _host;
    private UIElement? _content;
    private bool _visible;
    private bool _active;
    private double _actualWidth;
    private double _actualHeight;
    private double _scale = 1;
    private double _opacity = 1;
    private ScreenInfo? _screen;
    private WindowBackdrop _backdrop;
    private WindowCorners _corners;
    private IReadOnlyList<Rect>? _inputRegion;
    private Rect? _visibleMask;
    private bool _disposed;

    private protected ShellWindow(Shell shell)
    {
        _shell = shell;
    }

    public static ShellWindow? Of(UIElement element)
    {
        return (element.XamlRoot?.Content as FrameworkElement)?.Tag as ShellWindow;
    }

    private protected abstract WindowKind Kind { get; }

    internal HostWindow? Host => _host;

    public UIElement? Content
    {
        get => _content;
        set
        {
            if (!Set(ref _content, value))
                return;
            if (_host is not null)
                _host.Presentation.Content = value;
        }
    }

    public bool Visible
    {
        get => _visible;
        set
        {
            if (_disposed || !Set(ref _visible, value))
                return;
            if (value)
                EnsureHost();
            if (_host is not null)
            {
                ApplyPlacement();
                _host.Placement.SetVisible(value);
            }
        }
    }

    public bool Active => _active;

    public double Width
    {
        get;
        set
        {
            if (Set(ref field, value))
                OnPlacementChanged();
        }
    } = double.NaN;

    public double Height
    {
        get;
        set
        {
            if (Set(ref field, value))
                OnPlacementChanged();
        }
    } = double.NaN;

    public double ActualWidth => _actualWidth;

    public double ActualHeight => _actualHeight;

    public double Scale => _scale;

    public Point Origin => _host?.Placement.Origin ?? default;

    public ScreenInfo? Screen
    {
        get => _screen;
        set
        {
            if (Set(ref _screen, value))
                OnPlacementChanged();
        }
    }

    public double Opacity
    {
        get => _opacity;
        set
        {
            if (Set(ref _opacity, Math.Clamp(value, 0, 1)))
                _host?.Presentation.SetOpacity(_opacity);
        }
    }

    public WindowBackdrop Backdrop
    {
        get => _backdrop;
        set
        {
            if (Set(ref _backdrop, value))
                _host?.Presentation.SetBackdrop(_backdrop, _corners);
        }
    }

    public WindowCorners Corners
    {
        get => _corners;
        set
        {
            if (Set(ref _corners, value))
                _host?.Presentation.SetBackdrop(_backdrop, _corners);
        }
    }

    public IReadOnlyList<Rect>? InputRegion
    {
        get => _inputRegion;
        set
        {
            _inputRegion = value;
            Changed();
            _host?.Placement.SetInputRegion(value);
        }
    }

    public Rect? VisibleMask
    {
        get => _visibleMask;
        set
        {
            if (Set(ref _visibleMask, value))
                _host?.Presentation.SetVisibleMask(value);
        }
    }

    public event Action? Closed;

    internal event Action? HostCreated;

    internal event Action? Disposing;

    public event Action<Exception>? Failed;

    public void Focus()
    {
        _host?.Focus.TakeKeyboard();
    }

    public void Raise()
    {
        _host?.Placement.Raise();
    }

    public void StartSystemMove()
    {
        _host?.Placement.StartSystemMove();
    }

    public void StartSystemResize(Edges edges)
    {
        _host?.Placement.StartSystemResize(edges);
    }

    public (int X, int Y) MapToScreen(double x, double y)
    {
        return _host?.Placement.MapToScreen(x, y) ?? ((int)x, (int)y);
    }

    private void EnsureHost()
    {
        if (_host is not null)
            return;
        _host = _shell.Platform.CreateWindow(Kind, this);
        _host.Presentation.Content = _content;
        _host.Presentation.SetBackdrop(_backdrop, _corners);
        if (_opacity != 1)
            _host.Presentation.SetOpacity(_opacity);
        if (_inputRegion is not null)
            _host.Placement.SetInputRegion(_inputRegion);
        if (_visibleMask is { } mask)
            _host.Presentation.SetVisibleMask(mask);
        ApplyFocus();
        HostCreated?.Invoke();
    }

    internal abstract IWindowPlacer CreatePlacer(HostWindow host, ShellPlatform platform);

    private protected abstract void ApplyPlacement();

    private protected virtual void ApplyFocus()
    {
    }

    private protected void OnPlacementChanged()
    {
        if (_host is not null)
            ApplyPlacement();
    }

    internal virtual void OnDismissed()
    {
    }

    internal virtual void OnStateChanged(bool maximized, bool minimized)
    {
    }

    internal virtual void OnFullscreenChanged(bool fullscreen)
    {
    }

    internal void OnResized(double width, double height, double scale)
    {
        Set(ref _actualWidth, width, nameof(ActualWidth));
        Set(ref _actualHeight, height, nameof(ActualHeight));
        Set(ref _scale, scale, nameof(Scale));
    }

    internal void OnMoved()
    {
        Changed(nameof(Origin));
    }

    internal void OnClosed()
    {
        Visible = false;
        Closed?.Invoke();
    }

    internal void OnActiveChanged(bool active)
    {
        Set(ref _active, active, nameof(Active));
    }

    internal void OnScreenChanged(string screenId)
    {
        if (_screen is null && _shell.Platform.Screens.Any(s => s.Id == screenId))
            Changed(nameof(Screen));
    }

    internal void OnBackingDestroyed()
    {
        _host = null;
        if (Set(ref _visible, false, nameof(Visible)))
            Closed?.Invoke();
    }

    internal void OnFailed(Exception error)
    {
        _shell.Log.Error("window", error.ToString());
        var host = _host;
        _host = null;
        host?.Dispose();
        Failed?.Invoke(error);
        if (_visible && !_disposed)
        {
            EnsureHost();
            ApplyPlacement();
            _host!.Placement.SetVisible(true);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Disposing?.Invoke();
        _host?.Dispose();
        _host = null;
        Set(ref _visible, false, nameof(Visible));
        GC.SuppressFinalize(this);
    }
}
