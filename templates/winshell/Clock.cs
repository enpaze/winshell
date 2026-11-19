namespace MyShell;

public sealed class Clock : Widget
{
    public override Element Render()
    {
        var time = UseNow(TimeSpan.FromSeconds(1));
        return TextBlock($"{time:t}").AutomationName("Clock");
    }
}
