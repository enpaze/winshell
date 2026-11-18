using Windows.Win32.Foundation;

namespace Winshell.Native;

internal static class TextBuffer
{
    internal readonly record struct Answer(WIN32_ERROR Code, int Length);

    internal readonly record struct Terminated(WIN32_ERROR Code, uint Length);

    internal static string? Read(int firstCapacity, int maxCharacters, Func<char[], Answer> query)
    {
        var capacity = firstCapacity;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var buffer = new char[capacity];
            var answer = query(buffer);
            if (answer.Code == WIN32_ERROR.ERROR_SUCCESS)
                return (uint)answer.Length > (uint)buffer.Length ? null : new string(buffer, 0, answer.Length);
            if (attempt == 1 || answer.Code != WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER || answer.Length <= capacity ||
                answer.Length > maxCharacters)
                return null;
            capacity = answer.Length;
        }

        return null;
    }

    internal static string ReadTerminated(int maxCharacters, Func<char[], Terminated> query)
    {
        return Read(0, maxCharacters, buffer =>
        {
            var answer = query(buffer);
            if (answer.Code != WIN32_ERROR.ERROR_SUCCESS)
                return new Answer(answer.Code, (int)answer.Length);
            return new Answer(answer.Code, answer.Length > (uint)buffer.Length ? -1 : (int)answer.Length - 1);
        }) ?? "";
    }
}
