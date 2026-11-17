namespace Winshell.Services.Tests;

internal sealed class FakeInstanceControl : IInstanceControl
{
    public string? ReloadError { get; init; }

    public Exception? InspectFailure { get; init; }

    public int Reloads { get; private set; }

    public int Quits { get; private set; }

    public int Yields { get; private set; }

    public Action? OnYield { get; set; }

    public string? BuildError { get; private set; }

    public string? BuildProject { get; private set; }

    public ShellIpc Ipc { get; } = new(new Log());

    public string? Reload(string? program)
    {
        Reloads++;
        return ReloadError;
    }

    public void Quit()
    {
        Quits++;
    }

    public void Yield()
    {
        Yields++;
        OnYield?.Invoke();
    }

    public void ShowBuildError(string error, string projectFile)
    {
        BuildError = error;
        BuildProject = projectFile;
    }

    public void DismissNotice()
    {
    }

    public Task<string> Inspect(string command, IReadOnlyList<string> args)
    {
        return InspectFailure is null ? Task.FromResult(command) : Task.FromException<string>(InspectFailure);
    }
}
