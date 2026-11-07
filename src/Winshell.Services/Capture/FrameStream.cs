using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Vortice.Direct3D11;
using Vortice.Mathematics;
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

    private readonly object _gate = new();
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
            _item = CaptureInterop.CreateItem(target.Window, monitor);
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
                // A resized target draws into the old buffers until the pool takes the new size. A frame that grew
                // past them is cut off, so it is dropped, and the next comes at the new size.
                var size = frame.ContentSize;
                if (size.Width > 0 && size.Height > 0 &&
                    (size.Width != _poolSize.Width || size.Height != _poolSize.Height))
                {
                    var grew = size.Width > _poolSize.Width || size.Height > _poolSize.Height;
                    _poolSize = size;
                    _ = Task.Run(Restart);
                    if (grew)
                        return;
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

    /// <summary>The frame's content in memory.</summary>
    private PixelImage? Copy(Direct3D11CaptureFrame frame)
    {
        using var texture = CaptureInterop.Texture(frame.Surface);
        var desc = texture.Description;
        var width = (int)Math.Min(desc.Width, (uint)Math.Max(0, frame.ContentSize.Width));
        var height = (int)Math.Min(desc.Height, (uint)Math.Max(0, frame.ContentSize.Height));
        if (width == 0 || height == 0)
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

        _context!.CopySubresourceRegion(staging, 0, 0, 0, 0, texture, 0, new Box(0, 0, 0, width, height, 1));
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
