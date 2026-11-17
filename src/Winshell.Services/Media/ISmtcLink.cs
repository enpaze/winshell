using Windows.Foundation;
using Windows.Media.Control;

namespace Winshell.Media;

internal interface ISmtcLink
{
    void Start(SmtcSession target);

    void Retarget(GlobalSystemMediaTransportControlsSession session);

    void Release();

    void Send(string what, Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> command);
}
