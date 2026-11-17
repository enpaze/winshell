namespace Winshell.Session;

internal interface ISessionBackend
{
    bool Lock();

    bool SignOut();

    bool Restart();

    bool ShutDown();

    bool Sleep();

    bool Hibernate();

    bool SleepAvailable { get; }

    bool HibernateAvailable { get; }
}
