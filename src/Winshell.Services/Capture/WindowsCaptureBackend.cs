using Vortice.Direct3D11;
using Vortice.Mathematics;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Winshell.Native;

namespace Winshell.Capture;

/// <summary>
/// Samples the latest frame of a <see cref="CaptureLifetime"/>, so slow consumers never queue frames or UI callbacks.
/// Only a consumed frame leaves the GPU.
/// </summary>
/// <param name="log">Where a teardown that fails is reported.</param>
internal sealed class WindowsCaptureBackend(Log log) : ICaptureBackend
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
            return null; // GPU work is still in flight; never stall the shell's scene thread.
        Direct3D11CaptureFrame? latest = null;
        try
        {
            // At most the pool's buffers. Never wait for a producer or drain an unbounded queue.
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
            // The first frame after a resize may have old-sized storage. Drop it rather than export clipped pixels.
            if (size.Width != _size.Width || size.Height != _size.Height)
            {
                latest.Dispose();
                latest = null;
                _size = size;
                _staging?.Dispose();
                _staging = null;
                // A static source may not invalidate after the pool is recreated. Stop the old session and
                // release its frames before the pool goes away, then start a new pair.
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
        // An in-flight closed event then no-ops, instead of stopping a session that is already going away.
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
