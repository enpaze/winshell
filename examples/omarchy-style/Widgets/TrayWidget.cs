using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Winshell;
using Winshell.Tray;
using static Microsoft.UI.Reactor.Factories;

namespace OmarchyStyle;

/// <summary>
/// The tray, on the primary screen only. Hovering the chevron slides out the icons Windows keeps in its overflow:
/// to the side on a top or bottom bar, and downwards on a side bar, where the icons stack.
/// </summary>
public sealed class TrayWidget : Component<WidgetContext>
{
    public override Element Render()
    {
        if (Props.Window is null)
            return TextBlock("").IsVisible(false);
        return Component<Icons, IconsProps>(new(Props.Window, Props));
    }

    private sealed record IconsProps(ShellWindow Window, WidgetContext Context);

    /// <summary>The icons, once the bar's window is known. Only the primary screen shows them.</summary>
    private sealed class Icons : Component<IconsProps>
    {
        private const double IconSize = 12;

        public override Element Render()
        {
            var window = UseObservable(Props.Window);
            var shell = Props.Context.Shell;
            var screens = UseObservable(shell.Screens);
            var items = UseObservable(shell.SystemTray.Items);
            var clip = UseRef<FrameworkElement?>(null);
            var hiddenPanel = UseRef<FrameworkElement?>(null);
            var open = UseRef(false);
            var vertical = Props.Context.Orientation == Orientation.Vertical;
            var screen = screens.FirstOrDefault(each => each.Id == window.Screen?.Id);
            var shown = screen is { IsPrimary: true } ? items.ToList() : [];

            void Slide()
            {
                if (clip.Current is not { } target || hiddenPanel.Current is not { } hidden)
                    return;
                var to = !open.Current ? 0 : vertical ? hidden.ActualHeight : hidden.ActualWidth;
                var slide = new DoubleAnimation
                {
                    To = to,
                    Duration = TimeSpan.FromMilliseconds(600),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    EnableDependentAnimation = true
                };
                Storyboard.SetTarget(slide, target);
                Storyboard.SetTargetProperty(slide, vertical ? nameof(FrameworkElement.Height) : nameof(FrameworkElement.Width));
                new Storyboard { Children = { slide } }.Begin();
            }

            var hiddenIcons = shown.Where(item => item.Overflow).Select(item => Icon(item, screen!.DevicePixelRatio)).ToArray();
            var pinnedIcons = shown.Where(item => !item.Overflow).Select(item => Icon(item, screen!.DevicePixelRatio)).ToArray();
            var hidden = BarLayout.Along(Props.Context.Orientation, 0, hiddenIcons)
                .HAlign(vertical ? HorizontalAlignment.Center : HorizontalAlignment.Left)
                .VAlign(VerticalAlignment.Top)
                .WithKey("hidden")
                .OnMount(panel =>
                {
                    hiddenPanel.Current = panel;
                    panel.SizeChanged += (_, _) =>
                    {
                        if (open.Current)
                            Slide();
                    };
                });
            // Closed, the clip is empty along the bar, and the hidden icons beyond it don't show.
            // A grid, as before: the hidden row keeps its size while the grid's own height or width hides it.
            var clipBox = Grid(columns: [GridSize.Star()], rows: [GridSize.Star()], hidden.Grid(0, 0))
                .WithKey("clip")
                .OnMount(element =>
                {
                    if (vertical)
                        element.Height = 0;
                    else
                        element.Width = 0;
                    clip.Current = element;
                });
            var chevron = Button(Glyphs.TrayChevron).AutomationName("Show hidden icons");
            // Transparent, not null, so the pointer over the gaps still counts as over the overflow.
            // The color is a hit target, not paint, so it stays off the theme tokens.
#pragma warning disable REACTOR_THEME_001
            var overflow = BarLayout.Along(Props.Context.Orientation, 0, chevron, clipBox)
                .Background("transparent")
#pragma warning restore REACTOR_THEME_001
                .WithKey("overflow")
                .IsVisible(hiddenIcons.Length > 0)
                .OnPointerEntered((_, _) =>
                {
                    open.Current = true;
                    Slide();
                })
                .OnPointerExited((_, _) =>
                {
                    open.Current = false;
                    Slide();
                });
            var pinned = BarLayout.Along(Props.Context.Orientation, 0, pinnedIcons).WithKey("pinned");
            var bar = BarLayout.Along(Props.Context.Orientation, 0, overflow, pinned)
                .IsVisible(shown.Count > 0);
            return vertical ? bar.HAlign(HorizontalAlignment.Center) : bar.HAlign(HorizontalAlignment.Left);
        }

        private static Element Icon(SystemTrayItem item, double scale)
        {
            var icon = item.IconAt((int)Math.Round(IconSize * scale));
            var source = icon?.ToImageSource();
            var tip = item.Tooltip.Length > 0 ? item.Tooltip : item.AppName;
            return Button("", () => item.Click())
                .ToolTip(tip)
                .AutomationName(item.AppName)
                .OnRightTapped((_, _) => item.Display())
                .WithKey(BarLayout.Key(item))
                .Set(button => button.Content = new Image { Width = IconSize, Height = IconSize, Source = source });
        }
    }
}
