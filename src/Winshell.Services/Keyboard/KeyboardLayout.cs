namespace Winshell.Keyboard;

/// <summary>One keyboard layout Windows has installed.</summary>
public sealed class KeyboardLayout
{
    internal KeyboardLayout(string id, string label, string name)
    {
        Id = id;
        Label = label;
        Name = name;
    }

    /// <summary>The id Windows stores for this layout, such as <c>00000409</c>. It does not change with the UI language.</summary>
    public string Id { get; }

    /// <summary>A short label, such as EN.</summary>
    public string Label { get; }

    /// <summary>The name Windows shows, such as United States-International.</summary>
    public string Name { get; }
}
