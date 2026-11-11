using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Winshell;

/// <summary>
/// The next start's look at the crash folder. A fatal report newer than the last clean exit is shown once.
/// The record is TOML in the state folder: <c>clean</c> is the last exit that was not a crash, and <c>shown</c>
/// is the report already shown.
/// </summary>
internal sealed partial class CrashReports
{
    /// <summary>The newest crash report that says Winshell closed, when it has not been shown and it is newer than the last clean exit.</summary>
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

    /// <summary>Records an exit that was not a crash.</summary>
    public static void MarkClean(string stateFile, DateTimeOffset when)
    {
        Update(stateFile, when, null);
    }

    /// <summary>Records that <paramref name="reportPath"/> was shown, so the next start does not show it again.</summary>
    public static void MarkShown(string stateFile, string reportPath)
    {
        Update(stateFile, null, reportPath);
    }

    /// <summary>
    /// Sets what is given and keeps the rest. Instances share the record, so one writer at a time holds it from the
    /// read to the write, and no mark is lost to another instance's save.
    /// </summary>
    private static void Update(string stateFile, DateTimeOffset? clean, string? shown)
    {
        // A mutex name has a fixed limit, so the path is hashed. A symbolic link uses its target, and the
        // comparison ignores case, so those names wait on one writer.
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
                // The writer that owned this exited before it let go. This writer owns it now.
                acquired = true;
            }

            // Every writer holds the mutex, so no other one can change the record between this read and write.
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

    /// <summary>Writes the record, or returns false when a hand save landed and the file was left as it is.</summary>
    private static void Put(string stateFile, DateTimeOffset? clean, string? shown, string? expected)
    {
        // Edit the file that is there. A file that is not TOML is replaced with the record alone.
        if (expected is null || !TomlDocument.TryParse(stateFile, expected, out var document, out _))
            document = TomlDocument.Empty(stateFile);
        if (clean is { } when)
            document.Set("clean", when.ToString("o", CultureInfo.InvariantCulture));
        if (shown is { Length: > 0 })
            document.Set("shown", shown);
        // File.WriteAllText writes UTF-8 without a byte order mark. Keep that.
        AtomicFile.Save(stateFile, document.ToString(), false, expected);
    }
}
