using System.Globalization;

namespace Winshell;

internal static class MacAddress
{
    public static string Format(ReadOnlySpan<byte> bytes)
    {
        return string.Join(':', bytes.ToArray().Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
    }
}
