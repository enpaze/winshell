using Microsoft.UI.Dispatching;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Winshell;
using static Microsoft.UI.Reactor.Factories;

namespace MyShell;

public sealed class Clock : Component<WidgetContext>
{
    public override Element Render()
    {
        var (time, setTime) = UseState(DateTime.Now);
        UseEffect(() =>
        {
            var timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (_, _) => setTime(DateTime.Now);
            timer.Start();
            return () => timer.Stop();
        }, false);
        return TextBlock($"{time:t}").AutomationName("Clock");
    }
}
