using System.Globalization;
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
        var observe = UseRef(new Observe());
        _ = UseObservable(observe.Current);
        var audio = UseObservable(Props.Shell.Audio);
        var device = audio.DefaultPlaybackDevice;
        var observers = device is null
            ? []
            : new[]
            {
                Observe.Of(device, observe.Current, BarLayout.Key(device), () =>
                    string.Create(CultureInfo.InvariantCulture,
                        $"{device.Description}\n{device.Muted}:{device.Volume}:{device.Properties.GetValueOrDefault("formFactor")}"))
            };
        var tip = device is null
            ? ""
            : device.Description + ": " + (device.Muted ? "muted" : $"{Math.Round(device.Volume * 100)}%");
        var button = Button(device is null ? "" : Glyphs.Audio(device), () => Props.Shell.Open("ms-settings:sound"))
            .ToolTip(tip)
            .AutomationName(tip)
            .IsVisible(device is not null);
        return Observe.Show(button, HorizontalAlignment.Left, observers);
    }
}
