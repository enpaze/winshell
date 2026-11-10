using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winshell;
using static Microsoft.UI.Reactor.Factories;

namespace MessageWidget;

/// <summary>
/// One line of text from the placement's <c>text</c> setting. A vertical area is refused. A plain text block takes
/// the shell's style. A press opens that text in a popup styled by the area, and once loaded the tooltip names the
/// screen.
/// </summary>
public sealed class Message : Component<WidgetContext>
{
    public override Element Render()
    {
        var popup = UseRef<PopupWindow?>(null);
        UseEffect(() => () =>
        {
            popup.Current?.Dispose();
            popup.Current = null;
        }, false);

        var text = TextOf(Props);
        if (Props.Orientation == Orientation.Vertical)
            throw new UnsupportedWidgetException("The message widget runs in a horizontal area.");

        var line = text + " ·";
        return TextBlock(line)
            .Padding(8, 0)
            .OnPointerPressed((_, _) => ShowPopup(popup, line))
            .OnMount(ShowScreen);
    }

    private static string TextOf(WidgetContext context)
    {
        if (context.Settings.TryGetValue("text", out var value) && value is string text)
            return text;
        throw new UnsupportedWidgetException("text must be a string.");
    }

    private void ShowPopup(Ref<PopupWindow?> popup, string line)
    {
        popup.Current?.Dispose();
        var body = new TextBlock { Text = line, Margin = new Thickness(12) };
        popup.Current = new PopupWindow { Content = Props.StylePopup(body), Visible = true };
    }

    private static void ShowScreen(FrameworkElement label)
    {
        void SetTooltip()
        {
            var screen = ShellWindow.Of(label)?.Screen;
            ToolTipService.SetToolTip(label, screen is null ? "primary screen" : screen.Name);
        }

        if (label.IsLoaded)
            SetTooltip();
        else
            label.Loaded += (_, _) => SetTooltip();
    }
}
