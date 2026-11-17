namespace Winshell.Tray;

internal sealed class TrayIcon
{
    public required string Key { get; init; }

    public nint Window { get; set; }

    public uint Id { get; set; }

    public Guid Guid { get; set; }

    public uint CallbackMessage { get; set; }

    public uint Version { get; set; }

    public string Tip { get; set; } = "";

    public bool Hidden { get; set; }

    public bool Overflow { get; set; } = true;

    public PixelImage? Image { get; set; }

    public IconResource? Resource { get; set; }

    public int ProcessId { get; set; }

    public string ProgramPath { get; set; } = "";
}
