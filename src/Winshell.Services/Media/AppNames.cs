using Winshell.Apps;

namespace Winshell.Media;

/// <summary>
/// The names of the apps behind media sessions, looked up off the loop and kept while the session list lives.
/// Lives on the UI thread.
/// </summary>
internal sealed class AppNames(IEventLoop loop, Log log, Func<string, string> describe, Action named)
{
    private readonly Dictionary<string, string> _names = [];

    /// <summary>
    /// The app's name. Until a lookup finds a better one, and when the lookup fails, it is
    /// <see cref="AppsService.FallbackName"/>. When a lookup finds a name, <c>named</c> runs on the loop.
    /// </summary>
    public string Get(string appId)
    {
        if (_names.TryGetValue(appId, out var name))
            return name;
        name = _names[appId] = AppsService.FallbackName(appId);
        _ = LookUpAsync(appId);
        return name;
    }

    private async Task LookUpAsync(string appId)
    {
        string name;
        try
        {
            name = await OwnThread.Run(() => describe(appId)).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A failed lookup keeps the fallback name.
        catch (Exception e)
#pragma warning restore CA1031
        {
            var message = e.Message;
            loop.Post(() => log.Warn("media", $"Cannot name {appId}: {message}"));
            return;
        }

        loop.Post(() =>
        {
            _names[appId] = name;
            named();
        });
    }
}
