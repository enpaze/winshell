namespace Winshell.Capture;

internal interface ICaptureBackend : IDisposable
{
    void Start(CaptureTarget target, bool includeCursor, Action closed);

    PixelImage? ReadLatest();
}
