namespace Winshell.Notifications;

internal sealed class NotificationAppIcons(IEventLoop loop, Log log, CancellationToken stop)
{
    private readonly HashSet<string> _logged = [];
    private readonly Lock _gate = new();

    public void Read(string appKey, string label, IAppLogoSource source, Action<string> landed)
    {
        _ = LandAsync(OwnThread.Run(() => Claim(appKey, label, source)), landed);
    }

    private async Task LandAsync(Task<string> claim, Action<string> landed)
    {
        var url = await claim.ConfigureAwait(false);
        loop.Post(() => landed(url));
    }

    private string Claim(string appKey, string label, IAppLogoSource source)
    {
        try
        {
            using var stream = source.Open(stop);
            if (stream is null)
                return "";
            var url = TempImageStore.Shared.Claim(stream, stop);
            if (url is null)
                LogOnce(appKey, label, TempImageStore.Refused);
            return url ?? "";
        }
        catch (OperationCanceledException)
        {
            return "";
        }
#pragma warning disable CA1031
        catch (Exception e)
#pragma warning restore CA1031
        {
            LogOnce(appKey, label, e.Message.Length > 0 ? e.Message : e.GetType().Name);
            return "";
        }
    }

    private void LogOnce(string appKey, string label, string reason)
    {
        lock (_gate)
        {
            if (!_logged.Add(appKey))
                return;
        }

        log.Warn("notifications", $"Cannot read the icon from {label}: {reason}");
    }
}
