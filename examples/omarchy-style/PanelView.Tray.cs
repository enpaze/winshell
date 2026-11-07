using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Winshell;
using Winshell.Tray;

namespace OmarchyStyle;

public sealed partial class PanelView
{
    private double SlotWidth => (double)Resources["SlotWidth"];
    private double IconSize => (double)Resources["IconSize"];
    private int _overflowCount;
    private bool _overflowOpen;

    private void ShowTray(Watcher watch)
    {
        var items = watch.Read(_shell.SystemTray.Items).Select(watch.Read).ToList();
        PinnedItems.Children.Clear();
        OverflowItems.Children.Clear();
        foreach (var item in items)
            (item.Overflow ? OverflowItems : PinnedItems).Children.Add(TrayButton(item));
        _overflowCount = OverflowItems.Children.Count;
        Overflow.Visibility = _overflowCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        Tray.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_overflowOpen)
            SlideOverflow(_overflowCount * SlotWidth);
    }

    private Button TrayButton(SystemTrayItem item)
    {
        var icon = item.IconAt((int)Math.Round(IconSize * _screen.DevicePixelRatio)) ?? item.Icon;
        var button = new Button
        {
            Style = (Style)Resources["PanelButton"],
            Content = new Image
            {
                Width = IconSize,
                Height = IconSize,
                Source = icon?.ToImageSource()
            }
        };
        AutomationProperties.SetName(button, item.AppName);
        SetToolTip(button, item.Tooltip.Length > 0 ? item.Tooltip : item.AppName);
        button.Click += (_, _) => item.Click();
        button.RightTapped += (_, _) => item.Display();
        return button;
    }

    private void OnOverflowEntered(object sender, RoutedEventArgs e)
    {
        _overflowOpen = true;
        SlideOverflow(_overflowCount * SlotWidth);
    }

    private void OnOverflowExited(object sender, RoutedEventArgs e)
    {
        _overflowOpen = false;
        SlideOverflow(0);
    }

    private void SlideOverflow(double width)
    {
        var slide = new DoubleAnimation
        {
            To = width,
            Duration = TimeSpan.FromMilliseconds(600),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(slide, OverflowClip);
        Storyboard.SetTargetProperty(slide, nameof(Width));
        var story = new Storyboard();
        story.Children.Add(slide);
        story.Begin();
    }
}
