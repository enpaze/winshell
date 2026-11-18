using Winshell.Apps;

namespace Winshell.Media;

internal sealed class AppNames(IEventLoop loop, Log log, Func<string, string> describe, Action named)
{
    private readonly Dictionary<string, string> _names = [];

    public string Get(string appId)
    {
        if (_names.TryGetValue(appId, out var name))
            return name;
        name = _names[appId] = AppIdentity.FallbackName(appId);
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
#pragma warning disable CA1031
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
