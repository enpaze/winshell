using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winshell;

namespace MessageWidget;

/// <summary>
/// One line of text from the placement's <c>text</c> setting. A vertical area is refused. The foreground follows the
/// area's theme. A press opens that text in a popup styled by the area, and once loaded the tooltip names the screen.
/// </summary>
public sealed partial class Message : IDisposable
{
    private readonly WidgetContext _context;
    private PopupWindow? _popup;
    private bool _disposed;

    public Message(WidgetContext context)
    {
        var text = TextOf(context);
        if (context.Orientation == Orientation.Vertical)
            throw new UnsupportedWidgetException("The message widget runs in a horizontal area.");

        InitializeComponent();
        _context = context;
        Label.Text = text + " ·";
        Label.PointerPressed += (_, _) => ShowPopup();
        Loaded += (_, _) => ShowScreen();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _popup?.Dispose();
        _popup = null;
    }

    private static string TextOf(WidgetContext context)
    {
        if (context.Settings.TryGetValue("text", out var value) && value is string text)
            return text;
        throw new UnsupportedWidgetException("text must be a string.");
    }

    private void ShowPopup()
    {
        if (_disposed)
            return;
        _popup?.Dispose();
        var body = new TextBlock { Text = Label.Text, Margin = new Thickness(12) };
        _popup = new PopupWindow { Content = _context.StylePopup(body), Visible = true };
    }

    private void ShowScreen()
    {
        if (_disposed)
            return;
        var screen = ShellWindow.Of(this)?.Screen;
        ToolTipService.SetToolTip(Label, screen is null ? "primary screen" : screen.Name);
    }
}
