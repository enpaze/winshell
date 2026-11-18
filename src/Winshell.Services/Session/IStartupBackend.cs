namespace Winshell.Session;

internal interface IStartupBackend
{
    SignInVerdict Read();

    bool Register();

    bool Unregister();
}
