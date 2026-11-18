namespace Winshell;

internal sealed class ReportedDeviceAction(
    Action<Action> post,
    Func<bool> removed,
    Action<string> changed,
    Action<string> failed,
    string runningProperty)
{
    private readonly DeviceAction _action = new(post);

    public bool Running => _action.Running;

    public string Error { get; private set; } = "";

    public Task RunAsync(Func<Task<string?>> work)
    {
        if (removed())
            return Task.CompletedTask;
        return _action.RunAsync(() =>
        {
            changed(runningProperty);
            SetError("");
        }, work, error =>
        {
            if (removed())
                return;
            SetError(error);
            if (error.Length > 0)
                failed(error);
        }, () =>
        {
            if (!removed())
                changed(runningProperty);
        });
    }

    private void SetError(string error)
    {
        if (Error == error)
            return;
        Error = error;
        changed(nameof(Error));
    }
}
