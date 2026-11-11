namespace Winshell.Audio;

/// <summary>
/// The keys of <see cref="AudioNode.Properties"/>. A device, a session and a shell read this one list, and the
/// spellings do not change.
/// </summary>
public static class AudioProperty
{
    /// <summary>A device's Windows endpoint id (<c>endpointId</c>).</summary>
    public const string EndpointId = "endpointId";

    /// <summary>The name to show (<c>friendlyName</c>).</summary>
    public const string FriendlyName = "friendlyName";

    /// <summary>The description Windows gives a device (<c>interfaceName</c>).</summary>
    public const string InterfaceName = "interfaceName";

    /// <summary><c>playback</c> or <c>recording</c> (<c>flow</c>).</summary>
    public const string Flow = "flow";

    /// <summary>The adapter a device is on (<c>adapter</c>).</summary>
    public const string Adapter = "adapter";

    /// <summary>An icon name, or a program's path for a session (<c>icon</c>).</summary>
    public const string Icon = "icon";

    /// <summary>A device's shape, such as <c>speaker</c> or <c>headphone</c> (<c>formFactor</c>).</summary>
    public const string FormFactor = "formFactor";

    /// <summary>A session's Windows id (<c>sessionId</c>).</summary>
    public const string SessionId = "sessionId";

    /// <summary>The program a session belongs to (<c>appName</c>).</summary>
    public const string AppName = "appName";

    /// <summary>The process id of a session's program (<c>processId</c>).</summary>
    public const string ProcessId = "processId";

    /// <summary>The file name of a session's program (<c>processName</c>).</summary>
    public const string ProcessName = "processName";
}
