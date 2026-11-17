using Vortice.Direct3D11;
using Vortice.Mathematics;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Winshell.Native;

namespace Winshell.Capture;

internal sealed class FrameStream : IDisposable
{
    private readonly Lock _gate = new();
    private readonly Log _log;
    private readonly Action<PixelImage, TimeSpan> _frame;
    private readonly Action<Exception?> _ended;
    private CaptureLifetime? _life;
    private ID3D11Texture2D? _staging;
    private SizeInt32 _poolSize;

    private RECT? _crop;

    public FrameStream(CaptureTarget target, Log log, Action<PixelImage, TimeSpan> frame, Action<Exception?> ended)
    {
        _log = log;
        _frame = frame;
        _ended = ended;
        if (!GraphicsCaptureSession.IsSupported())
            throw new WindowsRefusal("Windows Graphics Capture is unavailable in this session.");
        try
        {
            var monitor = target.Window == 0 ? CaptureLifetime.MonitorHandle(target.MonitorId) : 0;
            if (target.Window == 0 && monitor == 0)
                throw new WindowsRefusal("The screen is no longer connected. (Parameter 'target')");
            var item = target.Window == 0 ? CaptureLifetime.CaptureItem(0, monitor) : WindowItem(target.Window);
            _poolSize = item.Size;
            if (_poolSize.Width <= 0 || _poolSize.Height <= 0)
                throw new WindowsRefusal("The window or screen has nothing to capture.");
            _life = CaptureLifetime.Create(item, true, _log, OnFrameArrived, OnClosed);
            _life.Start();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public bool OnScreen =>
        _crop is not null;

    private GraphicsCaptureItem WindowItem(nint window)
    {
        try
        {
            return CaptureLifetime.CaptureItem(window, 0);
        }
        catch (WindowsRefusal) when (Screens.Of(window) is { } screen &&
                                     TopLevelWindows.TryFrameBounds(window, out var frame))
        {
            var bounds = screen.Bounds;
            var crop = new RECT(Math.Max(frame.left, bounds.left) - bounds.left,
                Math.Max(frame.top, bounds.top) - bounds.top,
                Math.Min(frame.right, bounds.right) - bounds.left,
                Math.Min(frame.bottom, bounds.bottom) - bounds.top);
            if (crop.Width <= 0 || crop.Height <= 0)
                throw new WindowsRefusal(
                    "Windows refused to capture the window by itself, and it is on no screen to capture instead.");
            _crop = crop;
            return CaptureLifetime.CaptureItem(0, screen.Handle);
        }
    }

    private void Restart()
    {
        try
        {
            lock (_gate)
            {
                if (_life is not { Pool: not null } life)
                    return;
                life.Restart(_poolSize);
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
                if (_life is not { } life || !ReferenceEquals(life.Pool, pool))
                    return;
                using var frame = pool.TryGetNextFrame();
                if (frame is null)
                    return;
                var size = frame.ContentSize;
                if (size is { Width: > 0, Height: > 0 } &&
                    (size.Width != _poolSize.Width || size.Height != _poolSize.Height))
                {
                    _poolSize = size;
                    _ = Task.Run(Restart);
                }

                if (Copy(life, frame) is { } image)
                    _frame(image, frame.SystemRelativeTime);
            }
        }
        catch (Exception e)
        {
            _ended(e);
        }
    }

    private PixelImage? Copy(CaptureLifetime life, Direct3D11CaptureFrame frame)
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
            staging = _staging = life.Device!.CreateTexture2D(desc);
        }

        life.Context!.CopySubresourceRegion(staging, 0, 0, 0, 0, texture, 0,
            new Box(left, top, 0, left + width, top + height, 1));
        var pixels = CaptureInterop.Read(life.Context, staging, width, height, true);
        return pixels is null ? null : new PixelImage(width, height, pixels, true);
    }

    public void Dispose()
    {
        IDisposable? release;
        ID3D11Texture2D? staging;
        lock (_gate)
        {
            staging = _staging;
            _staging = null;
            release = _life?.Detach(staging);
            _life = null;
        }

        if (release is not null)
            release.Dispose();
        else
            CaptureLifetime.DisposeQuiet(staging, _log);
    }
}
