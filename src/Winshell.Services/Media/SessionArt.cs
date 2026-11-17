using Windows.Storage.Streams;

namespace Winshell.Media;

internal sealed class SessionArt(IEventLoop loop, Action<string> missed, Action changed)
{
    private readonly NewestRead _read = new();
    private bool _missLogged;
    private bool _released;

    public string Url { get; private set; } = "";

    public bool Loading => _read.Running;

    public void Load(IRandomAccessStreamReference? thumbnail)
    {
        var request = _read.Start();
        if (thumbnail is null)
        {
            Set("", request);
            return;
        }

        _ = Read();

        async Task Read()
        {
            try
            {
                var stream = await thumbnail.OpenReadAsync().AsTask().ConfigureAwait(false);
                var url = await Task.Run(() =>
                {
                    using (stream)
                    using (var input = stream.AsStreamForRead())
                    {
                        return TempImageStore.Shared.Claim(input);
                    }
                }).ConfigureAwait(false);
                if (url is null)
                    Skip(TempImageStore.Refused);
                else
                    loop.Post(() => Set(url, request));
            }
#pragma warning disable CA1031
            catch (Exception e)
#pragma warning restore CA1031
            {
                Skip(e.Message);
            }
        }

        void Skip(string reason)
        {
            loop.Post(() =>
            {
                _read.End(request);
                if (_missLogged)
                    return;
                _missLogged = true;
                missed(reason);
            });
        }
    }

    public void Release()
    {
        _released = true;
        TempImageStore.Shared.ReleaseLater(Url);
    }

    private void Set(string url, int request)
    {
        if (_released || !_read.End(request))
        {
            TempImageStore.Shared.ReleaseLater(url);
            return;
        }

        if (url.Length > 0)
            _missLogged = false;
        if (url == Url)
        {
            TempImageStore.Shared.ReleaseLater(url);
            return;
        }

        TempImageStore.Shared.ReleaseLater(Url);
        Url = url;
        changed();
    }
}
