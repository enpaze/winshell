namespace Winshell.Privacy;

/// <summary>An app using the microphone, the camera or location right now.</summary>
public sealed class PrivacyApp
{
    internal PrivacyApp(string name, string path, bool packaged, bool thisProgram)
    {
        Name = name;
        Path = path;
        Packaged = packaged;
        ThisProgram = thisProgram;
    }

    /// <summary>The name to show for the app.</summary>
    public string Name { get; }

    /// <summary>The desktop program's path. Empty for a packaged app.</summary>
    public string Path { get; }

    /// <summary>Whether this is a packaged app. A packaged app has no path.</summary>
    public bool Packaged { get; }

    /// <summary>
    /// Whether this is the program the shell is running as. The shell's audio meter holds a microphone stream open,
    /// and Windows counts that as microphone use; this is how a configuration tells that use from another app's.
    /// </summary>
    public bool ThisProgram { get; }
}
