using System.Globalization;
using Microsoft.UI.Xaml;
using Winshell;

namespace OmarchyStyle;

/// <summary>The default playback device: headphones, or a speaker by volume.</summary>
public sealed class AudioWidget(WidgetContext context) : StatusWidget(context, "ms-settings:sound")
{
    protected override void Show()
    {
        var device = Read(Shell.Audio).DefaultPlaybackDevice;
        if (device is null)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Read(device);
        Visibility = Visibility.Visible;
        var level = device.Muted ? "muted" : Math.Round(device.Volume * 100).ToString(CultureInfo.CurrentCulture) + "%";
        Bar.Show(this, Glyphs.Audio(device), device.Description + ": " + level);
    }
}
