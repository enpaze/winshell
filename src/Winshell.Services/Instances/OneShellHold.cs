using System.Security.Cryptography;
using System.Text;

namespace Winshell;

internal readonly struct OneShellHold : IDisposable
{
    private readonly HeldMutex _held;

    private OneShellHold(HeldMutex held)
    {
        _held = held;
    }

    public bool Acquired => _held.Acquired;

    public static string Name(ShellLocations locations)
    {
        return @"Local\winshell-one-shell-" +
               Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(locations.LocalRoot.ToUpperInvariant())));
    }

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
