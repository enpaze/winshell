namespace Winshell.Privacy;

public sealed class PrivacyApp
{
    internal PrivacyApp(string name, string path, bool packaged, bool thisProgram)
    {
        Name = name;
        Path = path;
        Packaged = packaged;
        ThisProgram = thisProgram;
    }

    public string Name { get; }

    public string Path { get; }

    public bool Packaged { get; }

    public bool ThisProgram { get; }
}
