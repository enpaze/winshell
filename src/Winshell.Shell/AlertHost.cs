using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell;

internal sealed record AlertProps(Element Content, bool On);

internal sealed class AlertHost : Component<AlertProps>
{
    public override Element Render()
    {
        var style = UseContext(StyleHost.InEffect) ?? Shell.Current.Style;
        var shown = Props.On ? style : null;
        return Border(Props.Content).Set(border => StyleHost.ShowAlert(border, shown));
    }
}
