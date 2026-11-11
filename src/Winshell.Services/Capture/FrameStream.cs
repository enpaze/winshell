using Vortice.Direct3D11;
using Vortice.Mathematics;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Winshell.Native;

namespace Winshell.Capture;

/// <summary>
/// Every frame Windows Graphics Capture delivers for a target, in memory, for a recording. Unlike the sampling
/// <see cref="WindowsCaptureBackend"/>, it copies each frame out of the pool as it arrives, on the pool's own thread,
/// and gives it back at once, so held buffers never stall capture. Windows delivers a frame when the target changes.
/// </summary>
internal sealed class FrameStream : IDisposable
{
    private const DirectXPixelFormat Format = DirectXPixelFormat.B8G8R8A8UIntNormalized;

    private readonly Lock _gate = new();
    private readonly Log _log;
    private readonly Action<PixelImage, TimeSpan> _frame;
    private readonly Action<Exception?> _ended;
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDirect3DDevice? _winrtDevice;
    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _pool;
    private GraphicsCaptureSession? _session;
    private ID3D11Texture2D? _staging;
    private SizeInt32 _poolSize;

    // The window's place on its screen, when Windows refused the window and its screen is captured instead.
    private RECT? _crop;

    /// <summary>Starts capturing <paramref name="target"/>, with the pointer in the picture.</summary>
    /// <param name="target">The window or screen.</param>
    /// <param name="log">Where a teardown that fails is reported.</param>
    /// <param name="frame">
    /// Each frame, premultiplied, with the time the compositor drew it on the clock <see cref="TimeProvider.GetTimestamp"/>
    /// reads. Called on the pool's thread, one frame at a time.
    /// </param>
    /// <param name="ended">Called when the target closes, with null, or when a frame can't be read, with why.</param>
    public FrameStream(CaptureTarget target, Log log, Action<PixelImage, TimeSpan> frame, Action<Exception?> ended)
    {
        _log = log;
        _frame = frame;
        _ended = ended;
        if (!GraphicsCaptureSession.IsSupported())
            throw new NotSupportedException("Windows Graphics Capture is unavailable in this session.");
        try
        {
            var monitor = target.Window == 0 ? WindowsCaptureBackend.MonitorHandle(target.MonitorId) : 0;
            if (target.Window == 0 && monitor == 0)
                throw new ArgumentException("The screen is no longer connected.", nameof(target));
            _item = target.Window == 0 ? CaptureInterop.CreateItem(0, monitor) : WindowItem(target.Window);
            _poolSize = _item.Size;
            if (_poolSize.Width <= 0 || _poolSize.Height <= 0)
                throw new InvalidOperationException("The window or screen has nothing to capture.");
            (_device, _context) = WindowsCaptureBackend.CreateDevice();
            _winrtDevice = CaptureInterop.CreateDevice(_device);
            _item.Closed += OnClosed;
            StartSession();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Whether Windows refused to capture the window by itself, so its place on its screen is captured instead, with
    /// whatever covers it, from where it was when the capture started.
    /// </summary>
    public bool OnScreen =>
        // The crop is written once before this object is returned, so this read never overlaps that write.
        _crop is not null;

    /// <summary>
    /// A capture item for the window, or for its screen when Windows refuses the window. Windows Server 2025 refuses
    /// every tool window, which every Winshell panel is, though Windows 11 build 26200 takes them.
    /// </summary>
    private GraphicsCaptureItem WindowItem(nint window)
    {
        try
        {
            return CaptureInterop.CreateItem(window, 0);
        }
        catch (ArgumentException) when (Screens.Of(window) is { } screen &&
                                        TopLevelWindows.TryFrameBounds(window, out var frame))
        {
            var bounds = screen.Bounds;
            var crop = new RECT(Math.Max(frame.left, bounds.left) - bounds.left,
                Math.Max(frame.top, bounds.top) - bounds.top,
                Math.Min(frame.right, bounds.right) - bounds.left,
                Math.Min(frame.bottom, bounds.bottom) - bounds.top);
            if (crop.Width <= 0 || crop.Height <= 0)
                throw new InvalidOperationException(
                    "Windows refused to capture the window by itself, and it is on no screen to capture instead.");
            // Written once before StartCapture, so no frame callback can read the crop during this store.
            _crop = crop;
            return CaptureInterop.CreateItem(0, screen.Handle);
        }
    }

    /// <summary>A new pool of <see cref="_poolSize"/> and a session on it. Called under the gate, or before any frame.</summary>
    private void StartSession()
    {
        // Two buffers: each frame leaves the pool before the next is drawn into it.
        _pool = Direct3D11CaptureFramePool.CreateFreeThreaded(_winrtDevice, Format, 2, _poolSize);
        _pool.FrameArrived += OnFrameArrived;
        _session = _pool.CreateCaptureSession(_item);
        // Leave the system's capture border on. No borderless capability or consent bypass.
        _session.IsCursorCaptureEnabled = true;
        _session.StartCapture();
    }

    /// <summary>
    /// Starts again at the target's new size, off the pool's thread. A static source may not draw again after the pool
    /// alone is recreated, so the session starts again too, as <see cref="WindowsCaptureBackend"/> does.
    /// </summary>
    private void Restart()
    {
        try
        {
            lock (_gate)
            {
                if (_pool is not { } pool)
                    return;
                pool.FrameArrived -= OnFrameArrived;
                _session?.Dispose();
                pool.Dispose();
                StartSession();
            }
        }
        catch (Exception e)
        {
            _ended(e);
        }
    }

    private void OnClosed(GraphicsCaptureItem sender, object args)
    {
        _ended(null);
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool pool, object args)
    {
        try
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_pool, pool))
                    return;
                using var frame = pool.TryGetNextFrame();
                if (frame is null)
                    return;
                // A resized target draws into the old buffers until the pool takes the new size.
                var size = frame.ContentSize;
                if (size is { Width: > 0, Height: > 0 } &&
                    (size.Width != _poolSize.Width || size.Height != _poolSize.Height))
                {
                    _poolSize = size;
                    _ = Task.Run(Restart);
                }

                if (Copy(frame) is { } image)
                    _frame(image, frame.SystemRelativeTime);
            }
        }
        catch (Exception e)
        {
            // Nothing may go back into Windows' thread. The recording ends and says why.
            _ended(e);
        }
    }

    /// <summary>
    /// The frame's content in memory, or the window's place in it, or null when its buffer, made for an older size, cuts
    /// that off. The next frame comes at the new size.
    /// </summary>
    private PixelImage? Copy(Direct3D11CaptureFrame frame)
    {
        using var texture = CaptureInterop.Texture(frame.Surface);
        var desc = texture.Description;
        var content = new RECT(0, 0, frame.ContentSize.Width, frame.ContentSize.Height);
        var (left, top, width, height) = _crop is { } crop
            ? (crop.left, crop.top, crop.Width, crop.Height)
            : (0, 0, content.Width, content.Height);
        if (width <= 0 || height <= 0 || left + width > Math.Min(content.Width, desc.Width) ||
            top + height > Math.Min(content.Height, desc.Height))
            return null;
        if (_staging is not { } staging || staging.Description.Width != width || staging.Description.Height != height)
        {
            _staging?.Dispose();
            desc.Width = (uint)width;
            desc.Height = (uint)height;
            desc.Usage = ResourceUsage.Staging;
            desc.BindFlags = BindFlags.None;
            desc.CPUAccessFlags = CpuAccessFlags.Read;
            desc.MiscFlags = ResourceOptionFlags.None;
            desc.ArraySize = 1;
            desc.MipLevels = 1;
            staging = _staging = _device!.CreateTexture2D(desc);
        }

        _context!.CopySubresourceRegion(staging, 0, 0, 0, 0, texture, 0,
            new Box(left, top, 0, left + width, top + height, 1));
        var pixels = CaptureInterop.Read(_context, staging, width, height, true);
        return pixels is null ? null : new PixelImage(width, height, pixels, true);
    }

    /// <summary>Stops capture. A frame being copied is delivered first, and none comes after this returns.</summary>
    public void Dispose()
    {
        IDisposable?[] resources;
        lock (_gate)
        {
            if (_item is { } item)
                item.Closed -= OnClosed;
            if (_pool is { } pool)
                pool.FrameArrived -= OnFrameArrived;
            resources = [_session, _pool, _staging, _winrtDevice, _context, _device];
            _item = null;
            _session = null;
            _pool = null;
            _staging = null;
            _winrtDevice = null;
            _context = null;
            _device = null;
        }

        foreach (var resource in resources)
            try
            {
                resource?.Dispose();
            }
            catch (Exception e) when (CaptureFailure.IsExpected(e))
            {
                _log.Warn("capture", $"Recording teardown: {e.Message}");
            }
    }
}
