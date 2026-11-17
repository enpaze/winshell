namespace Winshell.Keyboard;

public sealed class KeyboardLayout
{
    internal KeyboardLayout(string id, string label, string name)
    {
        Id = id;
        Label = label;
        Name = name;
    }

    public string Id { get; }

    public string Label { get; }

    public string Name { get; }
}
