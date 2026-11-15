using System.Security.Cryptography;
using System.Text;

namespace Winshell;

/// <summary>
/// Mutex for the one shell in a local folder. The shell holds it until it exits, so two starts that both pass the
/// instance list, because neither has written it yet, cannot both keep running. Owned until disposed, on the thread
/// that claimed it. A failed claim owns nothing.
/// </summary>
internal readonly struct OneShellHold : IDisposable
{
    private readonly HeldMutex _held;

    private OneShellHold(HeldMutex held)
    {
        _held = held;
    }

    public bool Acquired => _held.Acquired;

    /// <summary>The mutex for every shell that uses <paramref name="locations"/>, whichever shell it is.</summary>
    public static string Name(ShellLocations locations)
    {
        return @"Local\winshell-one-shell-" +
               Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(locations.LocalRoot.ToUpperInvariant())));
    }

    /// <summary>Claims the mutex, unless a shell already holds it. A mutex a crashed shell abandoned is claimed.</summary>
    public static OneShellHold TryClaim(ShellLocations locations)
    {
        var held = HeldMutex.TryClaim(Name(locations));
        return held.Acquired ? new OneShellHold(held) : default;
    }

    public void Dispose()
    {
        _held.Dispose();
    }
}
