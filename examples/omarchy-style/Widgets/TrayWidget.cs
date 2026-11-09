using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Winshell;
using Winshell.Tray;

namespace OmarchyStyle;

/// <summary>
/// The tray, on the primary screen only. Hovering the chevron slides out the icons Windows keeps in its overflow:
/// to the side on a top or bottom bar, and downwards on a side bar, where the icons stack.
/// </summary>
public sealed class TrayWidget : UserControl
{
    private const double IconSize = 12;

    public TrayWidget(WidgetContext context)
    {
        var shell = context.Shell;
        var vertical = context.Orientation == Orientation.Vertical;
        var hidden = new StackPanel
        {
            Orientation = context.Orientation,
            HorizontalAlignment = vertical ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        // Closed, the clip is empty along the bar, and the hidden icons beyond it don't show.
        var clip = new Grid { Children = { hidden } };
        if (vertical)
            clip.Height = 0;
        else
            clip.Width = 0;
        var chevron = new Button { Content = Glyphs.TrayChevron };
        AutomationProperties.SetName(chevron, "Show hidden icons");
        // Transparent, not null, so the pointer over the gaps still counts as over the overflow.
        var overflow = new StackPanel
        {
            Orientation = context.Orientation,
            Background = new SolidColorBrush(Colors.Transparent),
            Children = { chevron, clip }
        };
        var pinned = new StackPanel { Orientation = context.Orientation };
        Content = new StackPanel { Orientation = context.Orientation, Children = { overflow, pinned } };
        if (vertical)
            HorizontalAlignment = HorizontalAlignment.Center;

        var open = false;
        void Slide()
        {
            var slide = new DoubleAnimation
            {
                To = !open ? 0 : vertical ? hidden.ActualHeight : hidden.ActualWidth,
                Duration = TimeSpan.FromMilliseconds(600),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(slide, clip);
            Storyboard.SetTargetProperty(slide, vertical ? nameof(Height) : nameof(Width));
            new Storyboard { Children = { slide } }.Begin();
        }

        overflow.PointerEntered += (_, _) =>
        {
            open = true;
            Slide();
        };
        overflow.PointerExited += (_, _) =>
        {
            open = false;
            Slide();
        };
        // An icon that comes or goes while the overflow is open changes how far it slides.
        hidden.SizeChanged += (_, _) =>
        {
            if (open)
                Slide();
        };

        this.Watch(() =>
        {
            // The bar's window keeps the screen it was made for, so the screen as it is now, primary or not and at
            // what scale, is the one in shell.Screens. Null until the widget is in its window; the watch runs again
            // when it loads.
            var id = ShellWindow.Of(this)?.Screen?.Id;
            var screen = shell.Screens.FirstOrDefault(each => each.Id == id);
            var items = screen is { IsPrimary: true } ? shell.SystemTray.Items.ToList() : [];
            Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            hidden.Children.Clear();
            pinned.Children.Clear();
            foreach (var item in items)
                (item.Overflow ? hidden : pinned).Children.Add(TrayButton(item, screen!.DevicePixelRatio));
            overflow.Visibility = hidden.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private static Button TrayButton(SystemTrayItem item, double scale)
    {
        // Icon first: IconAt doesn't report a read, and reading Icon makes the watch follow a new icon.
        var icon = item.Icon;
        icon = item.IconAt((int)Math.Round(IconSize * scale)) ?? icon;
        var button = new Button
        {
            Content = new Image { Width = IconSize, Height = IconSize, Source = icon?.ToImageSource() }
        };
        var tip = item.Tooltip.Length > 0 ? item.Tooltip : item.AppName;
        ToolTipService.SetToolTip(button, tip);
        AutomationProperties.SetName(button, item.AppName);
        button.Click += (_, _) => item.Click();
        button.RightTapped += (_, _) => item.Display();
        return button;
    }
}
