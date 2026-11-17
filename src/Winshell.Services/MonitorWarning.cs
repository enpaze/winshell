namespace Winshell;

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
