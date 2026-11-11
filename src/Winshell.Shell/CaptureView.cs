using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Winshell.Capture;

namespace Winshell;

/// <summary>
/// A live view of a <see cref="CaptureSession"/>. The frame is fitted and centered, keeping its aspect ratio.
/// Its natural size is the frame's physical size divided by this window's scale. The frame is written once into
/// the bitmap, on the thread that delivered it, so the session's loop is this window's. The view lets go of the
/// session when it leaves the tree.
/// </summary>
public sealed partial class CaptureView : Panel
{
    /// <summary>The capture to show. Null shows nothing.</summary>
    public static readonly DependencyProperty SessionProperty = DependencyProperty.Register(
        nameof(Session),
        typeof(CaptureSession),
        typeof(CaptureView),
        new PropertyMetadata(null,
            (sender, args) =>
                ((CaptureView)sender).OnSessionChanged((CaptureSession?)args.NewValue)));

    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private CaptureSession? _subscribed;
    private readonly ShellWindowSubscription _shell;
    private int _frameWidth;
    private int _frameHeight;

    /// <summary>A view with no session, which shows nothing.</summary>
    public CaptureView()
    {
        _shell = new ShellWindowSubscription(OnShellChanged);
        Children.Add(_image);
        Loaded += (_, _) => OnLoaded();
        Unloaded += (_, _) => OnUnloaded();
    }

    /// <summary>The capture to show. Null shows nothing.</summary>
    public CaptureSession? Session
    {
        get => (CaptureSession?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    /// <summary>
    /// Asks for the frame's natural size, scaled down to fit <paramref name="availableSize"/> with its aspect ratio
    /// kept. Zero without a frame.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = Fit(Natural(), availableSize);
        _image.Measure(size);
        return size;
    }

    /// <summary>Gives the frame all of <paramref name="finalSize"/>; the frame is fitted and centered in it.</summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _image.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        return finalSize;
    }

    private static Size Fit(Size natural, Size available)
    {
        if (natural.Width <= 0 || natural.Height <= 0)
            return new Size(0, 0);
        var width = double.IsInfinity(available.Width) ? natural.Width : Math.Min(natural.Width, available.Width);
        var height = double.IsInfinity(available.Height) ? natural.Height : Math.Min(natural.Height, available.Height);
        var fit = Math.Min(width / natural.Width, height / natural.Height);
        return new Size(natural.Width * fit, natural.Height * fit);
    }

    private void OnLoaded()
    {
        _shell.Attach(this);
        Subscribe(Session);
    }

    private void OnUnloaded()
    {
        Subscribe(null);
        _shell.Detach();
    }

    private void OnSessionChanged(CaptureSession? next)
    {
        if (IsLoaded)
            Subscribe(next);
    }

    private void Subscribe(CaptureSession? session)
    {
        if (ReferenceEquals(_subscribed, session))
        {
            Show(session?.Frame);
            return;
        }

        if (_subscribed is not null)
            _subscribed.PropertyChanged -= OnFrameChanged;
        _subscribed = session;
        if (session is not null)
            session.PropertyChanged += OnFrameChanged;
        Show(session?.Frame);
    }

    private void OnFrameChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _subscribed) && e.PropertyName is nameof(CaptureSession.Frame) or null or "")
            Show(_subscribed?.Frame);
    }

    private void Show(PixelImage? frame)
    {
        if (frame is null)
        {
            if (_image.Source is null)
                return;
            _image.Source = null;
            _frameWidth = 0;
            _frameHeight = 0;
            InvalidateMeasure();
            return;
        }

        var resized = _frameWidth != frame.Width || _frameHeight != frame.Height;
        _frameWidth = frame.Width;
        _frameHeight = frame.Height;
        _image.Source = frame.ToImageSource();
        if (resized)
            InvalidateMeasure();
    }

    private Size Natural()
    {
        if (_frameWidth <= 0 || _frameHeight <= 0)
            return new Size(0, 0);
        var scale = ScaleOf();
        return new Size(_frameWidth / scale, _frameHeight / scale);
    }

    private double ScaleOf()
    {
        var scale = _shell.Window?.Scale ?? 0;
        if (scale <= 0)
            scale = XamlRoot?.RasterizationScale ?? 1;
        return scale > 0 ? scale : 1;
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellWindow.Scale))
            InvalidateMeasure();
    }
}
