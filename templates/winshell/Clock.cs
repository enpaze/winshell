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
        // UseObservable follows a service. The time is not one, so a timer renders the clock again each second.
        UseEffect(() =>
        {
            var timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (_, _) => setTime(DateTime.Now);
            timer.Start();
            return () => timer.Stop();
        }, false);
        // The name is how inspect and Narrator tell this text from any other. The text itself is only the time.
        return TextBlock($"{time:t}").AutomationName("Clock");
    }
}
