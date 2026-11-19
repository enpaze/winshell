using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MessageWidget;

public sealed class Message : Widget
{
    public override Element Render()
    {
        var popup = UseRef<PopupWindow?>(null);
        UseEffect(() => () =>
        {
            popup.Current?.Dispose();
            popup.Current = null;
        }, false);

        var screen = UseScreen();
        var text = Setting("text", "Hello");
        if (Orientation == Orientation.Vertical)
            throw new UnsupportedWidgetException("The message widget runs in a horizontal area.");

        var line = text + " ·";
        return TextBlock(line)
            .Padding(8, 0)
            .ToolTip(screen is null ? "primary screen" : screen.Name)
            .OnPointerPressed((_, _) => ShowPopup(popup, line));
    }

    private void ShowPopup(Ref<PopupWindow?> popup, string line)
    {
        popup.Current?.Dispose();
        var body = new TextBlock { Text = line, Margin = new Thickness(12) };
        popup.Current = Shell.Add(new PopupWindow { Content = StylePopup(body), Visible = true });
    }
}
