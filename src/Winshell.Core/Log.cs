namespace Winshell;

public enum LogLevel
{
    Debug,

    Info,

    Warn,

    Error,

    Fatal
}

public sealed class Log
{
    public static readonly Log Default = new();

    private readonly Lock _lock = new();
    private Action<LogLevel, string>? _file;
    private LogLevel _minimum = LogLevel.Info;

    internal Log()
    {
    }

    internal Action<LogLevel, string, string>? Sink { get; init; }

    internal event Action<string, string>? FileErrorChanged;

    internal void ReportFileError(string path, string error)
    {
        FileErrorChanged?.Invoke(path, error);
    }

    public void Write(LogLevel level, string category, string message)
    {
        if (level < _minimum)
            return;
        if (Sink is { } sink)
        {
            sink(level, category, message);
            return;
        }

        var label = level switch
        {
            LogLevel.Debug => "DEBUG",
            LogLevel.Info => " INFO",
            LogLevel.Warn => " WARN",
            LogLevel.Error => "ERROR",
            _ => "FATAL"
        };
        var line = string.IsNullOrEmpty(category) ? $"{label}: {message}" : $"{label} {category}: {message}";
        lock (_lock)
        {
            _file?.Invoke(level, line);
            Console.Error.WriteLine(line);
        }
    }

    internal void WriteTo(Action<LogLevel, string> file, LogLevel minimum)
    {
        lock (_lock)
        {
            _file = file;
            _minimum = minimum;
        }
    }

    public void Debug(string category, string message)
    {
        Write(LogLevel.Debug, category, message);
    }

    public void Info(string category, string message)
    {
        Write(LogLevel.Info, category, message);
    }

    public void Warn(string category, string message)
    {
        Write(LogLevel.Warn, category, message);
    }

    public void Error(string category, string message)
    {
        Write(LogLevel.Error, category, message);
    }
}
