namespace Winshell;

/// <summary>
/// Warns about a failed poll once. The same failure is quiet until a poll succeeds or a different failure comes.
/// </summary>
/// <param name="log">Where the warning goes.</param>
/// <param name="category">The log category.</param>
/// <param name="what">What failed, put before the exception type and message; empty for those alone.</param>
internal sealed class MonitorWarning(Log log, string category, string what = "")
{
    private string? _last;

    public void Failed(Exception e)
    {
        var detail = $"{e.GetType().Name}: {e.Message}";
        if (detail == _last)
            return;
        _last = detail;
        log.Warn(category, what.Length == 0 ? detail : $"{what}: {detail}");
    }

    public void Succeeded()
    {
        _last = null;
    }
}
