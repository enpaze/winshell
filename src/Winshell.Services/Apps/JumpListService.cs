namespace Winshell.Apps;

public sealed class JumpListService
{
    private readonly IJumpListBackend _backend;
    private readonly Dictionary<string, AppJumpList> _byId = new(StringComparer.OrdinalIgnoreCase);

    internal JumpListService(IJumpListBackend? backend = null)
    {
        _backend = backend ?? new WindowsJumpListBackend();
    }

    public AppJumpList ForApp(string appId)
    {
        if (!_byId.TryGetValue(appId, out var list))
            _byId[appId] = list = new AppJumpList();
        if (string.IsNullOrWhiteSpace(appId))
        {
            list.Apply(false, []);
        }
        else
        {
            var read = _backend.Read(appId);
            list.Apply(read.Available, read.Names);
        }

        return list;
    }
}

public sealed class AppJumpList : ObservableObject
{
    private bool _available;
    private IReadOnlyList<string> _destinations = [];

    internal AppJumpList()
    {
    }

    public bool Available => Read(_available);

    public IReadOnlyList<string> Destinations => Read(_destinations);

    internal void Apply(bool available, IReadOnlyList<string> destinations)
    {
        Set(ref _available, available, nameof(Available));
        if (!_destinations.SequenceEqual(destinations, StringComparer.Ordinal))
            Set(ref _destinations, destinations.ToArray(), nameof(Destinations));
    }
}
