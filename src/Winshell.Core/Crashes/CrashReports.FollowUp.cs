using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Winshell;

internal sealed partial class CrashReports
{
    public static CrashRecord? Find(string crashFolder, string stateFile)
    {
        try
        {
            var reports = List(crashFolder).Reports;
            if (reports.Count == 0 || !reports[0].Closed)
                return null;
            var newest = reports[0];
            var state = ReadState(stateFile);
            if (state.Shown is not null &&
                string.Equals(state.Shown, newest.Path, StringComparison.OrdinalIgnoreCase))
                return null;
            if (state.Clean is { } clean && newest.Written <= clean)
                return null;
            return newest;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void MarkClean(string stateFile, DateTimeOffset when)
    {
        Update(stateFile, when, null);
    }

    public static void MarkShown(string stateFile, string reportPath)
    {
        Update(stateFile, null, reportPath);
    }

    private static void Update(string stateFile, DateTimeOffset? clean, string? shown)
    {
        var name = @"Local\winshell-exit-" + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(AtomicFile.FollowLink(stateFile).ToUpperInvariant())));
        using var mutex = new Mutex(false, name);
        var acquired = false;
        try
        {
            try
            {
                mutex.WaitOne();
                acquired = true;
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }

            var state = ReadState(stateFile);
            Put(stateFile, clean ?? state.Clean, shown ?? state.Shown, state.Text);
        }
        finally
        {
            if (acquired)
                mutex.ReleaseMutex();
        }
    }

    private readonly record struct ExitState(DateTimeOffset? Clean, string? Shown, string? Text);

    private static ExitState ReadState(string stateFile)
    {
        var load = TomlDocument.Load(stateFile);
        switch (load.Kind)
        {
            case TomlRead.Missing:
                return default;
            case TomlRead.Unreadable or TomlRead.TooLarge:
                throw new IOException(load.Message);
            case TomlRead.Invalid:
                return new ExitState(null, null, load.Text);
            case TomlRead.Parsed:
                var values = load.Document!.Values;
                DateTimeOffset? clean = null;
                if (values.GetValueOrDefault("clean") is string cleanText &&
                    DateTimeOffset.TryParse(cleanText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                        out var when))
                    clean = when;
                var shown = values.GetValueOrDefault("shown") as string;
                return new ExitState(clean, shown, load.Text);
            default:
                return default;
        }
    }

    private static void Put(string stateFile, DateTimeOffset? clean, string? shown, string? expected)
    {
        if (expected is null || !TomlDocument.TryParse(stateFile, expected, out var document, out _))
            document = TomlDocument.Empty(stateFile);
        if (clean is { } when)
            document.Set("clean", when.ToString("o", CultureInfo.InvariantCulture));
        if (shown is { Length: > 0 })
            document.Set("shown", shown);
        AtomicFile.Save(stateFile, document.ToString(), false, expected);
    }
}
