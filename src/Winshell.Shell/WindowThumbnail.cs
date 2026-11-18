using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Winshell.Native;
using Winshell.Windows;

namespace Winshell;

public sealed partial class WindowThumbnail : FrameworkElement
{
    public static readonly DependencyProperty WindowProperty = DependencyProperty.Register(
        nameof(Window),
        typeof(AppWindow),
        typeof(WindowThumbnail),
        new PropertyMetadata(null,
            (sender, args) =>
                ((WindowThumbnail)sender).OnWindowChanged((AppWindow?)args.OldValue, (AppWindow?)args.NewValue)));

    public static readonly DependencyProperty PeekProperty = DependencyProperty.Register(
        nameof(Peek),
        typeof(bool),
        typeof(WindowThumbnail),
        new PropertyMetadata(false, (sender, _) => ((WindowThumbnail)sender).UpdatePeek()));

    private readonly ShellWindowSubscription _shellWindow;
    private WindowPeek? _peeker;
    private nint _thumbnail;
    private nint _destination;
    private RECT _insets;
    private ThumbnailPaint _applied;

    public WindowThumbnail()
    {
        _shellWindow = new ShellWindowSubscription(OnShellWindowChanged);
        IsHitTestVisible = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        Loaded += (_, _) => OnLoaded();
        Unloaded += (_, _) => OnUnloaded();
        SizeChanged += (_, _) => UpdateThumbnail();
    }

    public AppWindow? Window
    {
        get => (AppWindow?)GetValue(WindowProperty);
        set => SetValue(WindowProperty, value);
    }

    public bool Peek
    {
        get => (bool)GetValue(PeekProperty);
        set => SetValue(PeekProperty, value);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        UpdateThumbnail();
        return size;
    }

    private void OnLoaded()
    {
        WatchClosed(Window, true);
        _shellWindow.Attach(this);
        UpdateThumbnail();
        UpdatePeek();
    }

    private void OnUnloaded()
    {
        WatchClosed(Window, false);
        _shellWindow.Detach();
        ReleaseThumbnail();
        _peeker?.End();
    }

    private void OnWindowChanged(AppWindow? previous, AppWindow? next)
    {
        if (IsLoaded)
        {
            WatchClosed(previous, false);
            WatchClosed(next, true);
        }

        _insets = default;
        ReleaseThumbnail();
        UpdateThumbnail();
        UpdatePeek();
    }

    private void WatchClosed(AppWindow? window, bool watch)
    {
        if (window is null)
            return;
        if (watch)
            window.Closed += OnClosed;
        else
            window.Closed -= OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Window = null;
    }

    private void OnShellWindowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellWindow.Origin) or nameof(ShellWindow.Scale) or nameof(ShellWindow.ActualWidth)
            or nameof(ShellWindow.ActualHeight))
            UpdateThumbnail();
    }

    private void UpdatePeek()
    {
        var hwnd = Peek && Window is { } window ? window.Hwnd : 0;
        if (hwnd == 0 && _peeker is null)
            return;
        _peeker ??= WindowPeek.ForSystem();
        _peeker.Show(hwnd, _shellWindow.Window?.Host?.Handle ?? 0);
    }

    private void UpdateThumbnail()
    {
        if (!IsLoaded || Window is not { Hwnd: > 0 } window)
        {
            ReleaseThumbnail();
            return;
        }

        var destination = _shellWindow.Window?.Host?.Handle ?? 0;
        if (destination == 0 || !TryBox(out var box))
        {
            HideThumbnail();
            return;
        }

        if (_thumbnail != 0 && _destination != destination)
            ReleaseThumbnail();
        if (_thumbnail == 0)
        {
            var thumbnail = DwmThumbnails.Register(destination, window.Hwnd);
            if (thumbnail == 0)
                return;
            _thumbnail = thumbnail;
            _destination = destination;
            _applied = default;
        }

        if (!DwmThumbnails.TrySourceSize(_thumbnail, out var width, out var height) || width <= 0 || height <= 0)
            return;
        var source = SourceRect(window.Hwnd, width, height);
        var paint = new ThumbnailPaint(
            FitPreview(box, source.Width, source.Height),
            source,
            OpacityOf(),
            true);
        if (paint == _applied)
            return;
        if (Apply(paint))
            _applied = paint;
    }

    private RECT SourceRect(nint hwnd, int width, int height)
    {
        if (!PInvoke.IsIconic((HWND)hwnd) && PInvoke.GetWindowRect((HWND)hwnd, out var window)
                                  && TopLevelWindows.TryFrameBounds(hwnd, out var frame))
        {
            var visible = PreviewSource(window, frame);
            _insets = new RECT(visible.left, visible.top, window.Width - visible.right, window.Height - visible.bottom);
        }

        var inset = new RECT(_insets.left, _insets.top, width - _insets.right, height - _insets.bottom);
        return inset is { Width: > 0, Height: > 0 } ? inset : new RECT(0, 0, width, height);
    }

    private bool TryBox(out RECT box)
    {
        box = default;
        if (Visibility != Visibility.Visible || ActualWidth <= 0 || ActualHeight <= 0 ||
            XamlRoot?.Content is not { } root)
            return false;
        GeneralTransform transform;
        try
        {
            transform = TransformToVisual(root);
        }
        catch (ArgumentException)
        {
            return false;
        }

        var scale = XamlRoot.RasterizationScale;
        if (scale <= 0)
            scale = 1;
        var top = transform.TransformPoint(default);
        var bottom = transform.TransformPoint(new Point(ActualWidth, ActualHeight));
        box = new RECT(
            (int)Math.Round(top.X * scale),
            (int)Math.Round(top.Y * scale),
            (int)Math.Round(bottom.X * scale),
            (int)Math.Round(bottom.Y * scale));
        return box is { Width: > 0, Height: > 0 };
    }

    private byte OpacityOf()
    {
        var opacity = 1.0;
        for (DependencyObject? node = this; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is not UIElement element)
                continue;
            if (element.Visibility != Visibility.Visible)
                return 0;
            opacity *= element.Opacity;
        }

        return (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255);
    }

    private void HideThumbnail()
    {
        if (_thumbnail == 0 || !_applied.Visible)
            return;
        var hidden = _applied with { Visible = false };
        if (Apply(hidden))
            _applied = hidden;
    }

    private bool Apply(ThumbnailPaint paint)
    {
        return DwmThumbnails.Update(_thumbnail, paint.Destination, paint.Source, paint.Opacity, paint.Visible);
    }

    private void ReleaseThumbnail()
    {
        if (_thumbnail != 0)
            DwmThumbnails.Unregister(_thumbnail);
        _thumbnail = 0;
        _destination = 0;
        _applied = default;
    }

    internal static RECT PreviewSource(RECT window, RECT frame)
    {
        var whole = new RECT(0, 0, Math.Max(0, window.Width), Math.Max(0, window.Height));
        if (frame.Width <= 0 || frame.Height <= 0 || window.Width <= 0 || window.Height <= 0)
            return whole;
        var left = Math.Clamp(frame.left - window.left, 0, window.Width);
        var top = Math.Clamp(frame.top - window.top, 0, window.Height);
        var right = Math.Clamp(frame.right - window.left, left, window.Width);
        var bottom = Math.Clamp(frame.bottom - window.top, top, window.Height);
        return right - left <= 0 || bottom - top <= 0 ? whole : new RECT(left, top, right, bottom);
    }

    internal static RECT FitPreview(RECT box, int sourceWidth, int sourceHeight)
    {
        if (box.Width <= 0 || box.Height <= 0 || sourceWidth <= 0 || sourceHeight <= 0)
            return default;
        var scale = Math.Min(1, Math.Min((double)box.Width / sourceWidth, (double)box.Height / sourceHeight));
        var width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        var height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        var left = box.left + (box.Width - width) / 2;
        var top = box.top + (box.Height - height) / 2;
        return new RECT(left, top, left + width, top + height);
    }

    private readonly record struct ThumbnailPaint(RECT Destination, RECT Source, byte Opacity, bool Visible);
}
