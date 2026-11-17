using Winshell.Native;

namespace Winshell.Keyboard;

internal readonly record struct InstalledLayout(string Id, string Language, string Name);

internal sealed record KeyboardSnapshot(IReadOnlyList<InstalledLayout> Layouts, string? CurrentId);

internal interface IKeyboardLayoutBackend
{
    KeyboardSnapshot Read();

    bool Use(string id);
}

internal sealed class KeyboardLayoutBackend : IKeyboardLayoutBackend
{
    private readonly KeyboardLayouts _windows = new();

    public KeyboardSnapshot Read()
    {
        var census = _windows.Read();
        var layouts = new InstalledLayout[census.Layouts.Count];
        for (var i = 0; i < layouts.Length; i++)
        {
            var layout = census.Layouts[i];
            layouts[i] = new InstalledLayout(layout.Id, layout.Language, layout.Name);
        }

        return new KeyboardSnapshot(layouts, census.CurrentId);
    }

    public bool Use(string id)
    {
        return _windows.Use(id);
    }
}
