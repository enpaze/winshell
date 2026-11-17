namespace Winshell.Notifications;

internal interface IAppLogoSource
{
    Stream? Open(CancellationToken cancellation);
}
