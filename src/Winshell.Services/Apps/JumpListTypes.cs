namespace Winshell.Apps;

internal readonly record struct JumpListRead(bool Available, IReadOnlyList<string> Names)
{
    public const int PerCategory = 10;

    public static JumpListRead Missing => new(false, []);

    public static JumpListRead From(IReadOnlyList<string>? recent, IReadOnlyList<string>? frequent)
    {
        if (recent is null && frequent is null)
            return Missing;
        var names = new List<string>();
        Add(recent);
        Add(frequent);
        return new JumpListRead(true, names);

        void Add(IReadOnlyList<string>? source)
        {
            if (source is null)
                return;
            var taken = 0;
            foreach (var name in source)
            {
                if (taken >= PerCategory)
                    return;
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                var shown = name.Trim();
                if (names.Exists(have => have.Equals(shown, StringComparison.OrdinalIgnoreCase)))
                    continue;
                names.Add(shown);
                taken++;
            }
        }
    }
}

internal interface IJumpListBackend
{
    JumpListRead Read(string appUserModelId);
}
