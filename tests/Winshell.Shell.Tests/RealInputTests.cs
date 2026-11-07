using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Winshell.Native;

namespace Winshell.ShellTests;

/// <summary>
/// <c>winshell inspect</c>'s real input commands against a real panel: each raises the WinUI event or look a person's
/// mouse or keyboard would, and the cursor ends where it started. A test skips when someone is using the PC, because
/// the commands then send nothing.
/// </summary>
[Trait("Category", "Slow")]
public sealed class RealInputTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    private static PanelWindow Panel(FrameworkElement content)
    {
        // Away from the top left, where other tests' panels and the probe's go.
        return new PanelWindow
        {
            Anchors = Edges.Bottom | Edges.Right,
            Margins = new Thickness(0, 0, 200, 200),
            Width = 260,
            Height = 100,
            Content = content,
            Visible = true
        };
    }

    private static Border Spot()
    {
        // Smaller than the panel, so the panel has room off it.
        return new Border
        {
            Name = "Spot",
            Width = 120,
            Height = 60,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Colors.Teal)
        };
    }

    private static async Task<string> Send(Shell shell, string command, params string[] args)
    {
        try
        {
            return await Inspector.Run(shell, command, args);
        }
        catch (IpcException e) when (e.Message.StartsWith("Someone used the mouse", StringComparison.Ordinal))
        {
            Assert.Skip(e.Message);
            throw;
        }
    }

    /// <summary>What the shell said when it refused, or empty when it sent the input.</summary>
    private static async Task<string> Refused(Shell shell, string command, params string[] args)
    {
        try
        {
            await Send(shell, command, args);
            return "";
        }
        catch (IpcException e)
        {
            return e.Message;
        }
    }

    private static POINT Cursor()
    {
        return PointerInput.Cursor() ?? throw new InvalidOperationException("Windows won't say where the cursor is.");
    }

    private static Task Signal(TaskCompletionSource signal)
    {
        return signal.Task.WaitAsync(Limit, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Point_raises_PointerEntered_and_leave_puts_the_cursor_back_with_PointerExited()
    {
        var (before, during, after) = await ShellFixture.On(async shell =>
        {
            var spot = Spot();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            spot.PointerEntered += (_, _) => entered.TrySetResult();
            spot.PointerExited += (_, _) => exited.TrySetResult();
            using var panel = Panel(spot);
            await ShellFixture.LaidOut(spot);
            var before = Cursor();
            try
            {
                await Send(shell, "point", "Spot");
                await Signal(entered);
                var during = Cursor();
                await Send(shell, "leave");
                await Signal(exited);
                return (before, during, Cursor());
            }
            finally
            {
                await shell.Input.Leave();
            }
        });

        Assert.NotEqual((before.X, before.Y), (during.X, during.Y));
        Assert.Equal((before.X, before.Y), (after.X, after.Y));
    }

    [Fact]
    public async Task A_press_shows_the_pressed_look_clicks_and_puts_the_cursor_back()
    {
        var (states, clicks, before, after) = await ShellFixture.On(async shell =>
        {
            var button = new Button { Name = "Go", Content = "Go", Width = 200, Height = 60 };
            var clicked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var clicks = 0;
            button.Click += (_, _) =>
            {
                clicks++;
                clicked.TrySetResult();
            };
            using var panel = Panel(button);
            await ShellFixture.LaidOut(button);
            var states = new List<string>();
            var common = VisualStateManager.GetVisualStateGroups((FrameworkElement)VisualTreeHelper.GetChild(button, 0))
                .Single(group => group.Name == "CommonStates");
            common.CurrentStateChanged += (_, e) => states.Add(e.NewState.Name);
            var before = Cursor();

            await Send(shell, "press", "Go");
            await Signal(clicked);

            return (states, clicks, before, Cursor());
        });

        Assert.Contains("PointerOver", states);
        Assert.Contains("Pressed", states);
        Assert.Equal(1, clicks);
        Assert.Equal((before.X, before.Y), (after.X, after.Y));
    }

    [Fact]
    public async Task A_right_press_raises_RightTapped()
    {
        await ShellFixture.On(async shell =>
        {
            var spot = Spot();
            var tapped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            spot.RightTapped += (_, _) => tapped.TrySetResult();
            using var panel = Panel(spot);
            await ShellFixture.LaidOut(spot);

            await Send(shell, "press", "--right", "Spot");
            await Signal(tapped);
            return true;
        });
    }

    [Fact]
    public async Task A_scroll_turns_the_wheel_a_notch_a_step_toward_the_user()
    {
        var delta = await ShellFixture.On(async shell =>
        {
            var spot = Spot();
            var turned = 0;
            var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            spot.PointerWheelChanged += (_, e) =>
            {
                turned += e.GetCurrentPoint(spot).Properties.MouseWheelDelta;
                if (turned <= -240)
                    both.TrySetResult();
            };
            using var panel = Panel(spot);
            await ShellFixture.LaidOut(spot);

            await Send(shell, "scroll", "Spot", "2");
            await Signal(both);
            return turned;
        });

        Assert.Equal(-240, delta);
    }

    [Fact]
    public async Task Focus_tabs_to_the_element_so_it_has_keyboard_focus()
    {
        var state = await ShellFixture.On(async shell =>
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var name in (string[])["First", "Second", "Third"])
                row.Children.Add(new Button { Name = name, Content = name });
            using var panel = Panel(row);
            await ShellFixture.LaidOut(row);

            await Send(shell, "focus", "Third");
            return ((Button)row.Children[2]).FocusState;
        });

        Assert.Equal(FocusState.Keyboard, state);
    }

    [Fact]
    public async Task A_key_reaches_the_focused_control_in_a_window_that_is_not_in_front()
    {
        var clicks = await ShellFixture.On(async shell =>
        {
            var button = new Button { Name = "Go", Content = "Go" };
            var clicked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var clicks = 0;
            button.Click += (_, _) =>
            {
                clicks++;
                clicked.TrySetResult();
            };
            using var panel = Panel(button);
            await ShellFixture.LaidOut(button);
            button.Focus(FocusState.Programmatic);

            await Send(shell, "key", "Space");
            await Signal(clicked);
            return clicks;
        });

        Assert.Equal(1, clicks);
    }

    [Fact]
    public async Task A_chord_is_refused_while_the_window_is_not_in_front()
    {
        var message = await ShellFixture.On(async shell =>
        {
            var box = new TextBox { Name = "Field" };
            using var panel = Panel(box);
            await ShellFixture.LaidOut(box);
            return await Refused(shell, "key", "Ctrl+A");
        });

        Assert.StartsWith("Ctrl+A holds a modifier", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_element_under_another_window_gets_nothing_and_the_cursor_stays()
    {
        var (message, entered, before, after) = await ShellFixture.On(async shell =>
        {
            var spot = Spot();
            var entered = false;
            spot.PointerEntered += (_, _) => entered = true;
            using var below = Panel(spot);
            await ShellFixture.LaidOut(spot);
            var cover = new Grid { Background = new SolidColorBrush(Colors.Gray) };
            using var above = Panel(cover);
            await ShellFixture.LaidOut(cover);
            var before = Cursor();

            var refused = await Refused(shell, "point", "Spot");
            return (refused, entered, before, Cursor());
        });

        Assert.StartsWith("Another window is over Spot", message, StringComparison.Ordinal);
        Assert.False(entered);
        Assert.Equal((before.X, before.Y), (after.X, after.Y));
    }

    [Fact]
    public async Task An_element_clipped_out_of_sight_gets_nothing()
    {
        var message = await ShellFixture.On(async shell =>
        {
            // As a tray's overflow is before it slides out: the button has its size, in a slot of none.
            var hidden = new Button { Name = "Hidden", Content = "Hidden", Width = 80 };
            var clip = new Grid { Width = 0, HorizontalAlignment = HorizontalAlignment.Left };
            clip.Children.Add(hidden);
            var row = new Grid { Background = new SolidColorBrush(Colors.Teal) };
            row.Children.Add(clip);
            using var panel = Panel(row);
            await ShellFixture.LaidOut(hidden);

            return await Refused(shell, "point", "Hidden");
        });

        Assert.StartsWith("Hidden isn't what shows at its middle", message, StringComparison.Ordinal);
    }
}
