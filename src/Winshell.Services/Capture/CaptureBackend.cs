using Vortice.Direct3D11;
using Vortice.Mathematics;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Winshell.Native;

namespace Winshell.Capture;

internal sealed class CaptureBackend(Log log) : ICaptureBackend
{
    private CaptureLifetime? _life;
    private ID3D11Texture2D? _staging;
    private SizeInt32 _size;
    private bool _readbackPending;
    private Action? _closed;

    public void Start(CaptureTarget target, bool includeCursor, Action closed)
    {
        if (!GraphicsCaptureSession.IsSupported())
            throw new WindowsRefusal("Windows Graphics Capture is unavailable in this session.");
        _closed = closed;
        var monitor = target.Window == 0 ? CaptureLifetime.MonitorHandle(target.MonitorId) : 0;
        if (target.Window == 0 && monitor == 0)
            throw new WindowsRefusal("The selected monitor is no longer connected. (Parameter 'target')");
        var item = CaptureLifetime.CaptureItem(target.Window, monitor);
        _size = item.Size;
        if (_size.Width <= 0 || _size.Height <= 0)
            throw new WindowsRefusal("The selected source has no capture surface.");

        _life = CaptureLifetime.Create(item, includeCursor, log, null, OnClosed);
        _life.Start();
    }

    private void OnClosed(GraphicsCaptureItem sender, object args)
    {
        Volatile.Read(ref _closed)?.Invoke();
    }

    public PixelImage? ReadLatest()
    {
        var life = _life;
        if (life?.Pool is null)
            return null;
        var generation = life.Generation;
        var ready = _readbackPending ? Readback(life) : null;
        if (_readbackPending)
            return null;
        Direct3D11CaptureFrame? latest = null;
        try
        {
            for (var i = 0; i < CaptureLifetime.Buffers && generation == life.Generation; i++)
            {
                var pool = life.Pool;
                if (pool is null)
                    break;
                var frame = pool.TryGetNextFrame();
                if (frame is null)
                    break;
                latest?.Dispose();
                latest = frame;
            }

            if (generation != life.Generation)
            {
                latest?.Dispose();
                latest = null;
                return ready;
            }

            if (latest is null)
                return ready;
            var size = latest.ContentSize;
            if (size.Width <= 0 || size.Height <= 0)
                return ready;
            if (size.Width != _size.Width || size.Height != _size.Height)
            {
                latest.Dispose();
                latest = null;
                _size = size;
                _staging?.Dispose();
                _staging = null;
                life.Restart(size);
                return ready;
            }

            using var texture = CaptureInterop.Texture(latest.Surface);
            var desc = texture.Description;
            if (desc.Width < size.Width || desc.Height < size.Height)
                return ready;
            if (_staging is null)
            {
                desc.Width = (uint)size.Width;
                desc.Height = (uint)size.Height;
                desc.Usage = ResourceUsage.Staging;
                desc.BindFlags = BindFlags.None;
                desc.CPUAccessFlags = CpuAccessFlags.Read;
                desc.MiscFlags = ResourceOptionFlags.None;
                desc.ArraySize = 1;
                desc.MipLevels = 1;
                _staging = life.Device!.CreateTexture2D(desc);
            }

            life.Context!.CopySubresourceRegion(_staging, 0, 0, 0, 0, texture, 0,
                new Box(0, 0, 0, size.Width, size.Height, 1));
            _readbackPending = true;
            life.Context.Flush();
            return ready ?? Readback(life);
        }
        finally
        {
            latest?.Dispose();
        }
    }

    private PixelImage? Readback(CaptureLifetime life)
    {
        if (CaptureInterop.Read(life.Context!, _staging!, _size.Width, _size.Height) is not { } pixels)
            return null;
        _readbackPending = false;
        return new PixelImage(_size.Width, _size.Height, pixels, true);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _closed, null);
        var life = _life;
        var staging = _staging;
        _life = null;
        _staging = null;
        _readbackPending = false;
        if (life is null)
            CaptureLifetime.DisposeQuiet(staging, log);
        else
            life.Detach(staging).Dispose();
    }
}
