using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Winshell.Capture;

namespace Winshell;

public sealed partial class CaptureView : Panel
{
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
    private WriteableBitmap? _bitmap;

    public CaptureView()
    {
        _shell = new ShellWindowSubscription(OnShellChanged);
        Children.Add(_image);
        Loaded += (_, _) => OnLoaded();
        Unloaded += (_, _) => OnUnloaded();
    }

    public CaptureSession? Session
    {
        get => (CaptureSession?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = Fit(Natural(), availableSize);
        _image.Measure(size);
        return size;
    }

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
            if (_bitmap is null)
                return;
            _bitmap = null;
            _image.Source = null;
            InvalidateMeasure();
            return;
        }

        var resized = _bitmap is null || _bitmap.PixelWidth != frame.Width || _bitmap.PixelHeight != frame.Height;
        if (resized)
            _bitmap = new WriteableBitmap(frame.Width, frame.Height);
        frame.WriteInto(_bitmap!);
        if (!ReferenceEquals(_image.Source, _bitmap))
            _image.Source = _bitmap;
        if (resized)
            InvalidateMeasure();
    }

    private Size Natural()
    {
        if (_bitmap is null)
            return new Size(0, 0);
        var scale = ScaleOf();
        return new Size(_bitmap.PixelWidth / scale, _bitmap.PixelHeight / scale);
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
