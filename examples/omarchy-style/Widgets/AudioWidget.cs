using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Winshell;

namespace OmarchyStyle;

/// <summary>The default playback device: headphones, or a speaker by volume.</summary>
public sealed class AudioWidget : Button
{
    public AudioWidget(WidgetContext context)
    {
        var audio = context.Shell.Audio;
        this.Watch(() =>
        {
            var device = audio.DefaultPlaybackDevice;
            Visibility = device is null ? Visibility.Collapsed : Visibility.Visible;
            if (device is null)
                return;
            var tip = device.Description + ": " + (device.Muted ? "muted" : $"{Math.Round(device.Volume * 100)}%");
            Content = Glyphs.Audio(device);
            ToolTipService.SetToolTip(this, tip);
            AutomationProperties.SetName(this, tip);
        });
        Click += (_, _) => context.Shell.Open("ms-settings:sound");
    }
}
