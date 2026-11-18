using Microsoft.UI.Xaml;

namespace OmarchyStyle;

public sealed class AudioWidget : Widget
{
    public override Element Render()
    {
        var audio = UseObservableTree(Shell.Audio);
        var device = audio.DefaultPlaybackDevice;
        var tip = device is null
            ? ""
            : device.Description + ": " + (device.Muted ? "muted" : $"{Math.Round(device.Volume * 100)}%");
        return Button(device is null ? "" : Glyphs.Audio(device), () => Shell.Open("ms-settings:sound"))
            .ToolTip(tip)
            .AutomationName(tip)
            .AutomationId("audio")
            .IsVisible(device is not null)
            .HAlign(HorizontalAlignment.Left);
    }
}
