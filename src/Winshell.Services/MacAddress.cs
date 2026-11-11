using System.Globalization;

namespace Winshell;

/// <summary>A MAC address as the shell shows it: <c>XX:XX:XX:XX:XX:XX</c>.</summary>
internal static class MacAddress
{
    public static string Format(ReadOnlySpan<byte> bytes)
    {
        return string.Join(':', bytes.ToArray().Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
    }
}
