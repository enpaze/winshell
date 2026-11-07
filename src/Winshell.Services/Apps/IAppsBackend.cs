namespace Winshell.Apps;

/// <summary>
/// The Start menu's apps and their usage as Windows records them, and how Windows opens an app. Disposing it stops
/// following changes.
/// </summary>
internal interface IAppsBackend : IDisposable
{
    /// <summary>Raised on any thread when the apps or their usage may have changed. A read after it sees the change.</summary>
    event Action? Changed;

    /// <summary>Starts following changes. Does nothing when it already has.</summary>
    void Start();

    /// <summary>The apps and their usage, the apps combined and in name order. A read that blocks runs off the caller's thread.</summary>
    Task<AppSnapshot> ReadAsync();

    /// <summary>Opens a shortcut through the shell, in <paramref name="startIn"/> when that folder exists. Null when it opened.</summary>
    string? OpenShortcut(string path, string startIn);

    /// <summary>
    /// Activates a packaged app and returns at once. The task completes once the app is up, seconds on a cold start,
    /// with null or the error. It does not fault, and it completes on the activation's own thread, where a continuation
    /// can run without waiting for a pool worker.
    /// </summary>
    Task<string?> ActivateAsync(string appUserModelId, string arguments);

    /// <summary>Opens a packaged app through <c>shell:AppsFolder</c>. Null when it opened.</summary>
    string? OpenAppsFolder(string appUserModelId);
}
