using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Winshell;
using static Microsoft.UI.Reactor.Factories;

namespace OmarchyStyle;

/// <summary>The default playback device: headphones, or a speaker by volume.</summary>
public sealed class AudioWidget : Component<WidgetContext>
{
    public override Element Render()
    {
        var audio = UseObservableTree(Props.Shell.Audio);
        var device = audio.DefaultPlaybackDevice;
        var tip = device is null
            ? ""
            : device.Description + ": " + (device.Muted ? "muted" : $"{Math.Round(device.Volume * 100)}%");
        return Button(device is null ? "" : Glyphs.Audio(device), () => Props.Shell.Open("ms-settings:sound"))
            .ToolTip(tip)
            .AutomationName(tip)
            .IsVisible(device is not null)
            .HAlign(HorizontalAlignment.Left);
    }
}
