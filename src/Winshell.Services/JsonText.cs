using System.Text.Encodings.Web;
using System.Text.Json;

namespace Winshell;

internal static class JsonText
{
    private static JavaScriptEncoder Encoder { get; } = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    public static JsonWriterOptions IndentedWriter { get; } = new()
    {
        Indented = true,
        Encoder = Encoder
    };
}
