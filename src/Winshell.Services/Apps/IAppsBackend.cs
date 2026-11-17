namespace Winshell.Apps;

internal interface IAppsBackend : IDisposable
{
    event Action? Changed;

    void Start();

    Task<AppSnapshot> ReadAsync();

    string? OpenShortcut(string path, string startIn);

    Task<string?> ActivateAsync(string appUserModelId, string arguments);

    string? OpenAppsFolder(string appUserModelId);
}
