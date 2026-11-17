using Windows.Foundation;
using Winshell.Windows;

namespace Winshell.Capture;

public sealed class CaptureSession : ObservableObject, IDisposable
{
    private readonly IEventLoop _loop;
    private readonly Func<ICaptureBackend> _create;
    private ICaptureBackend? _backend;
    private IDisposable? _timer;
    private CaptureTarget? _target;
    private AppWindow? _watched;
    private PixelImage? _frame;
    private bool _includeCursor;
    private int _frameInterval = 33;
    private int _generation;
    private string _error = "";
    private bool _disposed;

    public CaptureSession(IEventLoop loop, Log log)
        : this(loop, () => new WindowsCaptureBackend(log))
    {
    }

    internal CaptureSession(IEventLoop loop, Func<ICaptureBackend> create)
    {
        _loop = loop;
        _create = create;
    }

    public event Action? FrameReady;

    public CaptureTarget? Target
    {
        get => Read(_target);
        set
        {
            if (_target == value && ReferenceEquals(_watched, value?.Source))
                return;
            Stop();
            _target = value;
            Watch(value?.Source);
            Changed();
        }
    }

    public bool Running => Read(_backend) is not null;

    public PixelImage? Frame => Read(_frame);

    public bool HasFrame => Read(_frame) is not null;

    public string Error => Read(_error);

    public Size PixelSize => new(Read(_frame)?.Width ?? 0, _frame?.Height ?? 0);

    public bool IncludeCursor
    {
        get => Read(_includeCursor);
        set => Set(ref _includeCursor, value);
    }

    public int FrameInterval
    {
        get => Read(_frameInterval);
        set => Set(ref _frameInterval, Math.Clamp(value, 16, 1000));
    }

    public bool Start()
    {
        if (_disposed)
            return false;
        Stop();
        Set(ref _error, "", nameof(Error));
        if (_target is not { } chosen)
        {
            Set(ref _error, "Choose a window or a screen before starting capture.", nameof(Error));
            return false;
        }

        try
        {
            var target = new CaptureTarget(chosen.Window, chosen.MonitorId);
            var generation = _generation;
            _backend = _create();
            _backend.Start(target, _includeCursor, () => _loop.Post(() =>
            {
                if (generation == _generation)
                    Stop();
            }));
            Changed(nameof(Running));
            if (generation == _generation && _backend is not null)
                Schedule(generation);
            return Running;
        }
        catch (Exception e) when (WindowsFailure.IsExpected(e))
        {
            Fail(e);
            return false;
        }
    }

    private void Schedule(int generation)
    {
        _timer = _loop.Schedule(TimeSpan.FromMilliseconds(_frameInterval), () =>
        {
            _timer = null;
            if (generation != _generation || _backend is null)
                return;
            try
            {
                if (_backend.ReadLatest() is { } frame)
                {
                    SetFrame(frame);
                    FrameReady?.Invoke();
                }
            }
            catch (Exception e) when (WindowsFailure.IsExpected(e))
            {
                Fail(e);
            }
            finally
            {
                if (generation == _generation && _backend is not null)
                    Schedule(generation);
            }
        });
    }

    public void Stop()
    {
        _generation++;
        _timer?.Dispose();
        _timer = null;
        var backend = _backend;
        _backend = null;
        try
        {
            backend?.Dispose();
        }
        catch (Exception e) when (WindowsFailure.IsExpected(e))
        {
            Set(ref _error, e.Message, nameof(Error));
        }
        finally
        {
            SetFrame(null);
            Changed(backend is not null, nameof(Running));
        }
    }

    public bool SaveToFile(string path)
    {
        if (_frame is null)
        {
            Set(ref _error, "No capture frame is available.", nameof(Error));
            return false;
        }

        if (LocalPath(path) is not { } local)
        {
            Set(ref _error, "Choose a local file path. (Parameter 'path')", nameof(Error));
            return false;
        }

        try
        {
            var png = _frame.EncodePng();
            using var stream = new FileStream(local, FileMode.CreateNew, FileAccess.Write);
            stream.Write(png);
            Set(ref _error, "", nameof(Error));
            return true;
        }
        catch (Exception e) when (WindowsFailure.IsExpected(e))
        {
            Set(ref _error, e.Message, nameof(Error));
            return false;
        }
    }

    public bool CopyToClipboard(nint owner)
    {
        if (_frame is null || owner == 0)
        {
            Set(ref _error, "A capture frame and an open window are required.", nameof(Error));
            return false;
        }

        try
        {
            CaptureClipboard.Copy(_frame, owner);
            Set(ref _error, "", nameof(Error));
            return true;
        }
        catch (Exception e) when (WindowsFailure.IsExpected(e))
        {
            Set(ref _error, e.Message, nameof(Error));
            return false;
        }
    }

    internal static string? LocalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (path.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            return Uri.TryCreate(path, UriKind.Absolute, out var uri) ? uri.LocalPath : null;
        return path.Contains("://", StringComparison.Ordinal) || path.StartsWith("data:", StringComparison.Ordinal)
            ? null
            : path;
    }

    private void SetFrame(PixelImage? frame)
    {
        var (hadFrame, size) = (HasFrame, PixelSize);
        Set(ref _frame, frame, nameof(Frame));
        Changed(hadFrame != HasFrame, nameof(HasFrame));
        Changed(size != PixelSize, nameof(PixelSize));
    }

    private void Fail(Exception error)
    {
        Stop();
        Set(ref _error, error.Message, nameof(Error));
    }

    private void Watch(AppWindow? window)
    {
        if (ReferenceEquals(_watched, window))
            return;
        if (_watched is not null)
            _watched.Closed -= OnWindowClosed;
        _watched = window;
        if (window is not null)
            window.Closed += OnWindowClosed;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _watched))
            Target = null;
    }

    public void Dispose()
    {
        _disposed = true;
        Stop();
        Watch(null);
    }
}
