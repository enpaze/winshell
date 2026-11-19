using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell;

internal sealed record AlertProps(Element Content, bool On);

internal sealed class AlertHost : Component<AlertProps>
{
    public override Element Render()
    {
        var scope = UseContext(StyleHost.InEffect);
        var (_, bump) = UseReducer(0);
        UseEffect(StyleHost.Follow(scope, () => bump(pulse => pulse + 1)), scope);
        var shown = Props.On ? scope?.Style() ?? Shell.Current.Style : null;
        return Border(Props.Content).Set(border => StyleHost.ShowAlert(border, shown));
    }
}
