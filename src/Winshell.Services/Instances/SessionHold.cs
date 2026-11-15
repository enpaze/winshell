using System.Security.Cryptography;
using System.Text;

namespace Winshell;

/// <summary>
/// Session mutex for the one shell in a local folder. The shell holds it until it exits, so two starts that both
/// pass the instance list, because neither has written it yet, cannot both keep running. Owned until disposed, on
/// the thread that claimed it. A failed claim owns nothing.
/// </summary>
internal readonly struct SessionHold : IDisposable
{
    private readonly Mutex? _mutex;

    private SessionHold(Mutex mutex)
    {
        _mutex = mutex;
    }

    public bool Acquired => _mutex is not null;

    /// <summary>The mutex for every shell that uses <paramref name="locations"/>, whichever shell id it has.</summary>
    public static string Name(ShellLocations locations)
    {
        return @"Local\winshell-session-" +
               Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(locations.LocalRoot.ToUpperInvariant())));
    }

    /// <summary>Claims the mutex, unless a shell already holds it. A mutex a crashed shell abandoned is claimed.</summary>
    public static SessionHold TryClaim(ShellLocations locations)
    {
        var mutex = new Mutex(false, Name(locations));
        bool mine;
        try
        {
            mine = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            mine = true;
        }

        if (!mine)
        {
            mutex.Dispose();
            return default;
        }

        return new SessionHold(mutex);
    }

    public void Dispose()
    {
        if (_mutex is null)
            return;
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
