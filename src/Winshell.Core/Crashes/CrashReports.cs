using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Winshell;

internal sealed record CrashReport(
    string Path,
    string Where,
    Exception Error,
    bool Fatal)
{
    public string Subject { get; init; } = "";

    public string Recovery { get; init; } = "";
}

internal readonly record struct CrashRecord(string Path, DateTimeOffset Written, bool Closed, string ExceptionLine);

internal readonly record struct CrashFileError(string Path, DateTimeOffset Written, string Message);

internal readonly record struct CrashList(
    IReadOnlyList<CrashRecord> Reports,
    string? FolderError,
    CrashFileError? FileError);

internal sealed partial class CrashReports(
    string folder,
    Log? log = null,
    string shellId = "",
    string configPath = "",
    string logPath = "")
{
    private const int Keep = 50;

    private const string Heading = "Winshell crash report";
    private const string ClosedLine = "Winshell closed: yes";
    private const string OpenLine = "Winshell closed: no";
    private const string ExceptionHeading = "Exception";
    private const int LogLines = 40;
    private const int LogBytes = 16 * 1024;

    private const string WritingFolder = "writing";

    private static readonly Lock TimeGate = new();
    private static long _lastTicks;
    private readonly Lock _lock = new();
    private readonly HashSet<string> _reported = [];

    public string Folder { get; } = Path.GetFullPath(folder);

    internal TimeProvider Time { get; init; } = TimeProvider.System;

    public event Action<CrashReport>? Written;

    public CrashReport? Write(string where, Exception error, bool fatal = false, string subject = "",
        string recovery = "")
    {
        CrashReport? report;
        lock (_lock)
        {
            report = WriteLocked(where, error, fatal, subject, recovery);
        }

        Raise(report);
        return report;
    }

    public CrashReport? WriteOnce(string where, Exception error, string subject = "", string recovery = "")
    {
        CrashReport? report;
        lock (_lock)
        {
            var fingerprint = Fingerprint(where, error);
            if (_reported.Contains(fingerprint))
                return null;
            report = WriteLocked(where, error, false, subject, recovery);
            if (report is not null)
                _reported.Add(fingerprint);
        }

        Raise(report);
        return report;
    }

    private void Raise(CrashReport? report)
    {
        if (report is null)
            return;
        try
        {
            Written?.Invoke(report);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            log?.Error("shell", $"Could not write a crash report: {e.Message}");
        }
    }

    private static string Fingerprint(string where, Exception error)
    {
        var text = new StringBuilder(where);

        void Add(Exception e, int depth)
        {
            text.Append('\n').Append(depth).Append(' ').Append(e.GetType().FullName).Append('\n').Append(e.StackTrace);
            if (depth >= 16)
                return;
            if (e is AggregateException aggregate)
                foreach (var inner in aggregate.InnerExceptions)
                    Add(inner, depth + 1);
            else if (e.InnerException is { } inner) Add(inner, depth + 1);
        }

        Add(error, 0);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private CrashReport? WriteLocked(string where, Exception error, bool fatal, string subject, string recovery)
    {
        try
        {
            var now = Time.GetLocalNow();
            var text = Format(now, where, error, fatal, recovery);
            var writing = Path.Combine(Folder, WritingFolder);
            Directory.CreateDirectory(writing);
            var temp = Path.Combine(writing, $"{Environment.ProcessId}-{Guid.NewGuid():N}.txt");
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true))
                    {
                        writer.Write(text);
                    }

                    stream.Flush(true);
                }

                var path = Path.Combine(Folder, FileName(NextTicks(now.UtcTicks), where));
                File.Move(temp, path, false);
                Prune(path);
                return new CrashReport(path, where, error, fatal) { Subject = subject, Recovery = recovery };
            }
            finally
            {
                try
                {
                    File.Delete(temp);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            log?.Error("shell", $"Could not write a crash report: {e.Message}");
            return null;
        }
    }

    private static long NextTicks(long utcTicks)
    {
        lock (TimeGate)
        {
            _lastTicks = Math.Max(utcTicks, _lastTicks + 1);
            return _lastTicks;
        }
    }

    private static string FileName(long utcTicks, string where)
    {
        return
            $"crash-{new DateTime(utcTicks, DateTimeKind.Utc).ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture)}Z-{Environment.ProcessId}-{Slug(where)}.txt";
    }

    [GeneratedRegex(@"^crash-(?<time>\d{8}-\d{6}-\d{7})Z-(?<pid>\d{1,10})-[a-z0-9\-]{1,40}\.txt$")]
    private static partial Regex ReportName();

    private readonly record struct ReportOrder(long Ticks, long Pid, string Name) : IComparable<ReportOrder>
    {
        public DateTimeOffset Written => new(Ticks, TimeSpan.Zero);

        public int CompareTo(ReportOrder other)
        {
            var order = (Ticks, Pid).CompareTo((other.Ticks, other.Pid));
            return order != 0 ? order : string.CompareOrdinal(Name, other.Name);
        }
    }

    private static ReportOrder? OrderOf(string name)
    {
        var m = ReportName().Match(name);
        if (!m.Success || !DateTime.TryParseExact(m.Groups["time"].Value, "yyyyMMdd-HHmmss-fffffff",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var time))
            return null;
        return new ReportOrder(time.Ticks, long.Parse(m.Groups["pid"].Value, CultureInfo.InvariantCulture), name);
    }

    private static string Slug(string text)
    {
        var slug = new StringBuilder();
        foreach (var c in text.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
                slug.Append(c);
            else if (slug.Length > 0 && slug[^1] != '-')
                slug.Append('-');
            if (slug.Length >= 40)
                break;
        }

        var result = slug.ToString().Trim('-');
        return result.Length > 0 ? result : "crash";
    }

    private string Format(DateTimeOffset time, string where, Exception error, bool fatal, string recovery)
    {
        var text = new StringBuilder();
        text.AppendLine(Heading);
        text.AppendLine("This file stays on this PC. Passwords, tokens and keys Winshell recognizes are taken out,");
        text.AppendLine("but read it before you attach it to a bug report: it has the paths of your shell.");
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"Time: {time:yyyy-MM-dd HH:mm:ss.fff zzz}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Winshell: {ProductVersion.Current.Text}");
        text.AppendLine(CultureInfo.InvariantCulture,
            $"Windows: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        text.AppendLine(CultureInfo.InvariantCulture, $".NET: {RuntimeInformation.FrameworkDescription}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Process: {Environment.ProcessId}");
        if (shellId.Length > 0)
            text.AppendLine(CultureInfo.InvariantCulture, $"Shell id: {shellId}");
        if (configPath.Length > 0)
            text.AppendLine(CultureInfo.InvariantCulture, $"Config: {configPath}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Where: {CrashRedaction.Redact(where)}");
        text.AppendLine(fatal ? ClosedLine : OpenLine);
        if (recovery.Length > 0)
            text.AppendLine(CultureInfo.InvariantCulture, $"Then: {recovery}");
        text.AppendLine();
        text.AppendLine(ExceptionHeading);
        text.AppendLine(CrashRedaction.Redact(Describe(error)));
        var tail = LogTail();
        if (tail.Count > 0)
        {
            text.AppendLine();
            text.AppendLine(CultureInfo.InvariantCulture, $"Log: {logPath}, last {tail.Count} lines");
            foreach (var line in tail)
                text.AppendLine(line);
        }

        return text.ToString();
    }

    private static string Describe(Exception error)
    {
        try
        {
            return error.ToString();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return $"{error.GetType().FullName}: (the exception could not be written: {e.GetType().Name})";
        }
    }

    private List<string> LogTail()
    {
        if (logPath.Length == 0 || !File.Exists(logPath))
            return [];
        List<string> lines;
        try
        {
            using var stream = SharedFile.OpenToRead(logPath);
            var start = Math.Max(0, stream.Length - LogBytes);
            stream.Seek(start, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            lines = reader.ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            if (start > 0 && lines.Count > 0)
                lines.RemoveAt(0);
            if (lines.Count > 0 && lines[^1].Length == 0)
                lines.RemoveAt(lines.Count - 1);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        return [.. lines.TakeLast(LogLines)];
    }

    private void Prune(string written)
    {
        try
        {
            var folder = new DirectoryInfo(Folder);
            if (folder.Attributes.HasFlag(FileAttributes.ReparsePoint) || folder.LinkTarget is not null)
                return;
            var reports = new List<(FileInfo File, ReportOrder Order)>();
            foreach (var file in folder.EnumerateFiles("crash-*.txt", SearchOption.TopDirectoryOnly))
            {
                if (file.Attributes.HasFlag(FileAttributes.ReparsePoint)
                    || !string.Equals(file.DirectoryName, Folder, StringComparison.OrdinalIgnoreCase)
                    || OrderOf(file.Name) is not { } order
                    || !StartsWithHeading(file.FullName))
                    continue;
                reports.Add((file, order));
            }

            var old = reports.OrderByDescending(r => r.Order).Skip(Keep);
            foreach (var (file, _) in old)
            {
                if (string.Equals(file.FullName, written, StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    file.Delete();
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static CrashList List(string folder)
    {
        if (!Directory.Exists(folder))
            return new CrashList([], null, null);
        try
        {
            var found = new List<(CrashRecord Record, ReportOrder Order)>();
            var failed = new List<(CrashFileError Error, ReportOrder Order)>();
            foreach (var file in new DirectoryInfo(folder).EnumerateFiles("crash-*.txt"))
            {
                if (OrderOf(file.Name) is not { } order)
                    continue;
                var read = ReadRecord(file.FullName, order.Written);
                if (read.Error is { } why)
                    failed.Add((new CrashFileError(file.FullName, order.Written, why), order));
                else if (read.Record is { } record)
                    found.Add((record, order));
            }

            var reports = found.OrderByDescending(r => r.Order).Select(r => r.Record).ToList();
            CrashFileError? fileError = failed.Count > 0 ? failed.MaxBy(r => r.Order).Error : null;
            return new CrashList(reports, null, fileError);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new CrashList([], $"{folder}: {e.Message}", null);
        }
    }

    private readonly record struct RecordRead(CrashRecord? Record, string? Error);

    private static RecordRead ReadRecord(string path, DateTimeOffset written)
    {
        try
        {
            using var stream = SharedFile.OpenToRead(path);
            using var reader = new StreamReader(stream);
            if (!IsHeading(reader.ReadLine()))
                return default;
            var closed = false;
            string? exception = null;
            var afterException = false;
            for (var i = 0; i < 40 && reader.ReadLine() is { } line; i++)
            {
                if (line == ClosedLine)
                    closed = true;
                if (afterException && exception is null && line.Length > 0)
                    exception = line;
                afterException = line == ExceptionHeading;
            }

            return new RecordRead(new CrashRecord(path, written, closed, exception ?? ""), null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new RecordRead(null, e.Message);
        }
    }

    private static bool IsHeading([NotNullWhen(true)] string? line)
    {
        return line == Heading;
    }

    private static bool StartsWithHeading(string path)
    {
        try
        {
            using var stream = SharedFile.OpenToRead(path);
            using var reader = new StreamReader(stream);
            return IsHeading(reader.ReadLine());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
