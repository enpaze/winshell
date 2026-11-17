using Vortice.Direct3D;
using Vortice.Direct3D11;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Winshell.Native;

namespace Winshell.Capture;

internal sealed class CaptureLifetime : IDisposable
{
    internal const int Buffers = 2;

    private const DirectXPixelFormat Format = DirectXPixelFormat.B8G8R8A8UIntNormalized;

    private readonly Log _log;
    private readonly bool _cursor;
    private readonly TypedEventHandler<Direct3D11CaptureFramePool, object>? _onFrame;
    private readonly TypedEventHandler<GraphicsCaptureItem, object>? _onClosed;
    private IDirect3DDevice? _winrt;
    private int _generation;
    private bool _closedHooked;

    private CaptureLifetime(GraphicsCaptureItem item, bool includeCursor, ID3D11Device device,
        ID3D11DeviceContext context, IDirect3DDevice winrt, Log log,
        TypedEventHandler<Direct3D11CaptureFramePool, object>? onFrame,
        TypedEventHandler<GraphicsCaptureItem, object>? onClosed)
    {
        Item = item;
        _cursor = includeCursor;
        Device = device;
        Context = context;
        _winrt = winrt;
        _log = log;
        _onFrame = onFrame;
        _onClosed = onClosed;
    }

    public GraphicsCaptureItem Item { get; }

    public ID3D11Device? Device { get; private set; }

    public ID3D11DeviceContext? Context { get; private set; }

    public Direct3D11CaptureFramePool? Pool { get; private set; }

    public GraphicsCaptureSession? Session { get; private set; }

    public int Generation => Volatile.Read(ref _generation);

    public static CaptureLifetime Create(GraphicsCaptureItem item, bool includeCursor, Log log,
        TypedEventHandler<Direct3D11CaptureFramePool, object>? onFrame,
        TypedEventHandler<GraphicsCaptureItem, object>? onClosed)
    {
        var (device, context) = CreateDevice();
        IDirect3DDevice? winrt = null;
        CaptureLifetime? life = null;
        try
        {
            winrt = CaptureInterop.CreateDevice(device);
            life = new CaptureLifetime(item, includeCursor, device, context, winrt, log, onFrame, onClosed);
            winrt = null;
            life.OpenPool(item.Size);
            return life;
        }
        catch
        {
            winrt?.Dispose();
            if (life is null)
            {
                context.Dispose();
                device.Dispose();
            }
            else
                life.Dispose();

            throw;
        }
    }

    public void Start()
    {
        Session!.StartCapture();
    }

    public void Restart(SizeInt32 size)
    {
        var session = Session;
        var pool = Pool;
        Session = null;
        Pool = null;
        Interlocked.Increment(ref _generation);
        UnhookFrame(pool);
        ReleaseSessionAndPool(session, pool, _log);
        OpenPool(size);
        Start();
    }

    public IDisposable Detach(ID3D11Texture2D? staging = null)
    {
        Interlocked.Increment(ref _generation);
        var pool = Pool;
        var release = new CaptureRelease(Session, pool, _winrt, Context, Device, staging, _log);
        Session = null;
        Pool = null;
        _winrt = null;
        Context = null;
        Device = null;
        try
        {
            UnhookFrame(pool);
            UnhookClosed();
            return release;
        }
        catch
        {
            release.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Detach().Dispose();
    }

    internal static GraphicsCaptureItem CaptureItem(nint window, nint monitor)
    {
        try
        {
            return CaptureInterop.CreateItem(window, monitor);
        }
        catch (ArgumentException e)
        {
            throw new WindowsRefusal(e.Message);
        }
    }

    internal static nint MonitorHandle(string device)
    {
        foreach (var handle in Monitors.List())
            if (Monitors.TryRead(handle, out var monitor) && monitor.Device == device)
                return handle;
        return 0;
    }

    private static (ID3D11Device Device, ID3D11DeviceContext Context) CreateDevice()
    {
        FeatureLevel[] levels = [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0];
        var result = D3D11.D3D11CreateDevice(null, DriverType.Hardware,
            DeviceCreationFlags.BgraSupport, levels, out var device, out ID3D11DeviceContext context);
        if (result.Failure)
            D3D11.D3D11CreateDevice(null, DriverType.Warp,
                DeviceCreationFlags.BgraSupport, levels, out device, out context).CheckError();
        return (device, context);
    }

    private void OpenPool(SizeInt32 size)
    {
        var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(_winrt, Format, Buffers, size);
        Pool = pool;
        if (_onFrame is not null)
            pool.FrameArrived += _onFrame;
        var session = pool.CreateCaptureSession(Item);
        Session = session;
        session.IsCursorCaptureEnabled = _cursor;
        if (!_closedHooked && _onClosed is not null)
        {
            Item.Closed += _onClosed;
            _closedHooked = true;
        }
    }

    private void UnhookFrame(Direct3D11CaptureFramePool? pool)
    {
        if (pool is not null && _onFrame is not null)
            pool.FrameArrived -= _onFrame;
    }

    private void UnhookClosed()
    {
        if (!_closedHooked || _onClosed is null)
            return;
        Item.Closed -= _onClosed;
        _closedHooked = false;
    }

    internal static void ReleaseSessionAndPool(GraphicsCaptureSession? session, Direct3D11CaptureFramePool? pool, Log log)
    {
        try
        {
            DisposeQuiet(session, log);
            try
            {
                for (var i = 0; i < Buffers && pool?.TryGetNextFrame() is { } frame; i++)
                    frame.Dispose();
            }
            catch (Exception e) when (WindowsFailure.IsExpected(e))
            {
                log.Warn("capture", $"Capture teardown: {e.Message}");
            }
        }
        finally
        {
            DisposeQuiet(pool, log);
        }
    }

    internal static void DisposeQuiet(IDisposable? resource, Log log)
    {
        try
        {
            resource?.Dispose();
        }
        catch (Exception e) when (WindowsFailure.IsExpected(e))
        {
            log.Warn("capture", $"Capture teardown: {e.Message}");
        }
    }

    private sealed class CaptureRelease(
        GraphicsCaptureSession? session,
        Direct3D11CaptureFramePool? pool,
        IDirect3DDevice? winrt,
        ID3D11DeviceContext? context,
        ID3D11Device? device,
        ID3D11Texture2D? staging,
        Log log) : IDisposable
    {
        public void Dispose()
        {
            try
            {
                ReleaseSessionAndPool(session, pool, log);
            }
            finally
            {
                DisposeQuiet(staging, log);
                DisposeQuiet(winrt, log);
                DisposeQuiet(context, log);
                DisposeQuiet(device, log);
            }
        }
    }
}
