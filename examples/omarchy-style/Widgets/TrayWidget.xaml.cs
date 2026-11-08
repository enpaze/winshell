using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Winshell;
using Winshell.Tray;

namespace OmarchyStyle;

/// <summary>
/// The tray on the primary screen. On any other screen the widget takes no space. A vertical area stacks the icons,
/// and the overflow slides out downwards.
/// </summary>
public sealed partial class TrayWidget : IDisposable
{
    private readonly Shell _shell;
    private readonly bool _vertical;
    private ScreenInfo? _screen;
    private Watcher? _watcher;
    private bool _disposed;
    private int _overflowCount;
    private bool _overflowOpen;

    public TrayWidget(WidgetContext context)
    {
        InitializeComponent();
        _vertical = context.Orientation == Orientation.Vertical;
        if (_vertical)
        {
            Tray.Orientation = Orientation.Vertical;
            Overflow.Orientation = Orientation.Vertical;
            OverflowItems.Orientation = Orientation.Vertical;
            OverflowItems.VerticalAlignment = VerticalAlignment.Top;
            OverflowItems.HorizontalAlignment = HorizontalAlignment.Center;
            PinnedItems.Orientation = Orientation.Vertical;
            OverflowClip.ClearValue(WidthProperty);
            OverflowClip.Height = 0;
            HorizontalAlignment = HorizontalAlignment.Center;
        }

        _shell = context.Shell;
        Loaded += (_, _) =>
        {
            if (_disposed)
                return;
            TrayChevron.Style = Bar.Resource<Style>(this, "PanelButton");
            TrayChevron.Content = Glyphs.TrayChevron;
            var screen = Bar.Screen(this, _shell);
            if (screen is null || !screen.IsPrimary)
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            _screen = screen;
            Visibility = Visibility.Visible;
            _watcher?.Dispose();
            _watcher = new Watcher(Show);
        };
    }

    public void Dispose()
    {
        _disposed = true;
        _watcher?.Dispose();
        _watcher = null;
    }

    private double SlotWidth => Bar.Resource<double>(this, "SlotWidth");

    private double IconSize => Bar.Resource<double>(this, "IconSize");

    private void Show(Watcher watch)
    {
        if (_screen is null)
            return;
        var items = watch.Read(_shell.SystemTray.Items).Select(watch.Read).ToList();
        PinnedItems.Children.Clear();
        OverflowItems.Children.Clear();
        foreach (var item in items)
            (item.Overflow ? OverflowItems : PinnedItems).Children.Add(TrayButton(item));
        _overflowCount = OverflowItems.Children.Count;
        Overflow.Visibility = _overflowCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        Tray.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_overflowOpen)
            SlideOverflow(OverflowExtent);
    }

    private Button TrayButton(SystemTrayItem item)
    {
        var icon = item.IconAt((int)Math.Round(IconSize * _screen!.DevicePixelRatio)) ?? item.Icon;
        var button = new Button
        {
            Style = Bar.Resource<Style>(this, "PanelButton"),
            Content = new Image
            {
                Width = IconSize,
                Height = IconSize,
                Source = icon?.ToImageSource()
            }
        };
        AutomationProperties.SetName(button, item.AppName);
        Bar.Tip(button, item.Tooltip.Length > 0 ? item.Tooltip : item.AppName, this);
        button.Click += (_, _) => item.Click();
        button.RightTapped += (_, _) => item.Display();
        return button;
    }

    private double OverflowExtent =>
        _overflowCount * (_vertical ? Bar.Resource<double>(this, "PanelHeight") : SlotWidth);

    private void OnOverflowEntered(object sender, RoutedEventArgs e)
    {
        _overflowOpen = true;
        SlideOverflow(OverflowExtent);
    }

    private void OnOverflowExited(object sender, RoutedEventArgs e)
    {
        _overflowOpen = false;
        SlideOverflow(0);
    }

    private void SlideOverflow(double extent)
    {
        var slide = new DoubleAnimation
        {
            To = extent,
            Duration = TimeSpan.FromMilliseconds(600),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(slide, OverflowClip);
        Storyboard.SetTargetProperty(slide, _vertical ? nameof(Height) : nameof(Width));
        var story = new Storyboard();
        story.Children.Add(slide);
        story.Begin();
    }
}
