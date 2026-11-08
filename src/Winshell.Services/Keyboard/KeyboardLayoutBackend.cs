using Winshell.Native;

namespace Winshell.Keyboard;

/// <summary>One installed layout as Windows reports it, before it has a short label.</summary>
internal readonly record struct InstalledLayout(string Id, string Language, string Name);

/// <summary>The installed layouts, in the order Windows cycles them, and the foreground window's layout id.</summary>
internal sealed record KeyboardSnapshot(IReadOnlyList<InstalledLayout> Layouts, string? CurrentId);

internal interface IKeyboardLayoutBackend
{
    /// <summary>Reads the installed layouts and the foreground window's layout.</summary>
    KeyboardSnapshot Read();

    /// <summary>Asks the foreground window to use the layout. Returns whether the request was posted.</summary>
    bool Use(string id);
}

/// <summary>The keyboard layouts Windows has installed, and a switch that reaches the foreground window.</summary>
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
