using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Winshell;

namespace OmarchyStyle;

/// <summary>
/// The panel on one screen: the Windows glyph and Winland's workspaces on the left, the clock in the middle, and the
/// tray and the status of Bluetooth, the network, audio, the screen and the battery on the right.
/// </summary>
public sealed partial class PanelView : IDisposable
{
    /// <summary>The panel's height, which it also reserves.</summary>
    public double Size => (double)Resources["PanelHeight"];

    private readonly Shell _shell;
    private readonly ScreenInfo _screen;
    private readonly List<Watcher> _watchers = [];
    private readonly DispatcherQueueTimer _clock;
    private readonly Style _toolTip;
    private readonly Brush _foreground;
    private readonly Brush _alert;

    public PanelView(Shell shell, ScreenInfo screen)
    {
        InitializeComponent();
        _shell = shell;
        _screen = screen;
        _toolTip = (Style)Resources["PanelToolTip"];
        _foreground = (Brush)Resources["PanelForeground"];
        _alert = (Brush)Resources["PanelAlert"];

        _clock = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _clock.IsRepeating = false;
        _clock.Tick += (_, _) => ShowTime();
    }

    private void OnLayoutLoaded(object sender, RoutedEventArgs e)
    {
        // Live XAML editing replaces the layout's elements, while this view and its clock stay alive.
        WindowsGlyph.Text = Glyphs.Windows;
        TrayChevron.Content = Glyphs.TrayChevron;
        ShowTime();

        foreach (var watcher in _watchers)
            watcher.Dispose();
        _watchers.Clear();
        _watchers.Add(new Watcher(ShowWorkspaces));
        if (_screen.IsPrimary)
            _watchers.Add(new Watcher(ShowTray));
        _watchers.Add(new Watcher(ShowBluetooth));
        _watchers.Add(new Watcher(ShowNetwork));
        _watchers.Add(new Watcher(ShowAudio));
        ShowScreen();
        _watchers.Add(new Watcher(ShowBattery));
    }

    public void Dispose()
    {
        _clock.Stop();
        foreach (var watcher in _watchers)
            watcher.Dispose();
    }

    private void ShowTime()
    {
        var now = DateTime.Now;
        Clock.Text = now.ToString("dddd HH:mm", CultureInfo.CurrentCulture);
        SetToolTip(Clock, now.ToString("D", CultureInfo.CurrentCulture));
        _clock.Interval = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond);
        _clock.Start();
    }

    private void Show(Button button, string glyph, string toolTip, bool alert = false)
    {
        button.Content = glyph;
        button.Foreground = alert ? _alert : _foreground;
        AutomationProperties.SetName(button, toolTip);
        SetToolTip(button, toolTip);
    }

    private void SetToolTip(DependencyObject element, string text)
    {
        if (ToolTipService.GetToolTip(element) is ToolTip tip)
            tip.Content = text;
        else
            ToolTipService.SetToolTip(element, new ToolTip { Content = text, Style = _toolTip });
    }
}
