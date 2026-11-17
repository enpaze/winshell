using System.Security.Cryptography;
using System.Text;

namespace Winshell;

internal readonly struct DuplicateHold : IDisposable
{
    private readonly HeldMutex _held;

    private DuplicateHold(HeldMutex held)
    {
        _held = held;
    }

    public bool Acquired => _held.Acquired;

    public static string ShellMutexName(ShellLocations locations, string shellId)
    {
        return @"Local\winshell-shell-" + Hash(locations.LocalRoot + "\n" + shellId);
    }

    private static string Hash(string text)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToUpperInvariant())));
    }

    public static DuplicateHold TryClaim(ShellLocations locations, string shellId)
    {
        var held = HeldMutex.TryClaim(ShellMutexName(locations, shellId));
        if (!held.Acquired)
            return default;

        if (locations.Instances.List(false)
            .Any(i => string.Equals(i.Info.ShellId, shellId, StringComparison.OrdinalIgnoreCase)))
        {
            held.Dispose();
            return default;
        }

        return new DuplicateHold(held);
    }

    public void Dispose()
    {
        _held.Dispose();
    }
}
