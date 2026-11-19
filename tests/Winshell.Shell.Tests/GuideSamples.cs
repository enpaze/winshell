using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Color = global::Windows.UI.Color;

namespace Winshell.ShellTests;

public static class GuideSamples
{
    private static IDisposable? _registration;

    public static void RunTheShell()
    {
        Shell.Run(shell => shell.Add(new Bar { Center = ["clock"] }));
    }

    public static void APanel(Shell shell)
    {
        shell.Add(new PanelWindow
        {
            Edge = Edge.Bottom,
            Align = PanelAlign.Stretch,
            Height = 48,
            Reserve = true,
            Content = new TextBlock { Text = "Winshell" }
        });
    }

    public static ScreenWindows ADockOnEachScreen(Shell shell)
    {
        var dock = shell.Add(new ScreenWindows("dock", (screen, row) => new PanelWindow
        {
            Screen = screen,
            Edge = row?.GetValueOrDefault("edge") is "top" ? Edge.Top : Edge.Bottom,
            Align = PanelAlign.Stretch,
            Height = 48,
            Reserve = true,
            Content = new TextBlock { Text = screen.Name }
        }));
        return dock;
    }

    public static void ANote(Shell shell)
    {
        _registration =
            shell.Ipc.Handle("notes", "hello", (string name) => "hi " + name);
        _ = _registration;
    }

    public static PanelWindow AWidgetArea(Shell shell)
    {
        var panel = shell.Add(new PanelWindow<EndArea> { Height = 48 });
        return panel;
    }

    public static (PopupWindow Calendar, Input.Hotkey Hotkey) AComponentPopup(Shell shell)
    {
        var calendar = shell.Add(new PopupWindow<Calendar> { Position = PopupPosition.Top, Width = 320, Height = 240 });
        var hotkey = shell.Hotkey("Win+Alt+C", calendar.Toggle);
        return (calendar, hotkey);
    }

    public static (PopupWindow Today, PopupWindow Later) TwoNotes(Shell shell)
    {
        var today = shell.Add(new PopupWindow<Note, string>("Stand-up at 10") { Position = PopupPosition.Top });
        var later = shell.Add(new PopupWindow<Note, string>("Pay the rent"));
        return (today, later);
    }

    public static (Bar Top, Bar Side) Bars(Shell shell)
    {
        var bar = shell.Add(new Bar
        {
            Edge = Edge.Top,
            Start = ["workspaces"],
            Center = ["clock"],
            End = ["tray", new("message", new Dictionary<string, object> { ["text"] = "Hello" })]
        });
        var side = shell.Add(new Bar { Name = "side", Edge = Edge.Left, Reserve = false, Start = ["clock"] });
        return (bar, side);
    }

    public static Launcher ALauncher(Shell shell)
    {
        var launcher = shell.Add(new Launcher
        {
            Hotkey = "Alt+Space",
            Commands =
            [
                new("Lock", () => shell.Session.Lock()),
                new("Reload shell", () => shell.Reload())
            ]
        });
        return launcher;
    }

    public static PanelWindow ASurface(Shell shell)
    {
        var dock = shell.Add(new PanelWindow<Dock> { Edge = Edge.Bottom, Width = 400, Height = 56 });
        return dock;
    }

    public static void AMovedStyle(Shell shell)
    {
        shell.Style = new ShellStyle
        {
            Background = Color.FromArgb(255, 0x1a, 0x1b, 0x26),
            FontFamily = new FontFamily("Segoe UI")
        };
    }

    public static void AStyle(Shell shell)
    {
        shell.Style = new ShellStyle
        {
            Background = Color.FromArgb(255, 0x1a, 0x1b, 0x26),
            Foreground = Color.FromArgb(255, 0xa9, 0xb1, 0xd6),
            Alert = Color.FromArgb(255, 0xf7, 0x76, 0x8e),
            FontFamily = new FontFamily("ms-appx:///Fonts/JetBrainsMonoNerdFont-Regular.ttf#JetBrainsMono NF"),
            FontSize = 12,
            SlotSize = 26,
            Padding = 8
        };
    }

    public static void AHotkey(Shell shell)
    {
        var hotkey = shell.Hotkey("Win+E", () => { });
    }

    public static Button ASettingsButton(Shell shell)
    {
        var sound = new Button { Content = "Sound" };
        sound.Click += (_, _) => shell.Open("ms-settings:sound");
        return sound;
    }

    public static DockPreferences ADock()
    {
        var dock = new DockPreferences("dock");
        return dock;
    }

    public static void Reload(Shell shell)
    {
        if (!shell.Reload())
            shell.Log.Warn("shell", "Reload did not start.");
    }
}

public sealed class Dock : Component
{
    public override Element Render() =>
        WidgetArea("dock", ["start", "tasks"]).Padding(4);
}

public sealed class Calendar : Component
{
    public override Element Render() => TextBlock("Today");
}

public sealed class Note : Component<string>
{
    public override Element Render() => TextBlock(Props);
}

public sealed class EndArea : Component
{
    public override Element Render() => WidgetArea("end",
    [
        new("message", new Dictionary<string, object> { ["text"] = "Hello" }),
        new("message", new Dictionary<string, object> { ["text"] = "World" })
    ]);
}

[WidgetName("hello")]
public sealed class Greeting : Widget
{
    public override Element Render() =>
        TextBlock(Setting("text", "Hello"));
}

public sealed class AlertLabel : Widget
{
    public override Element Render() => TextBlock("!").Alert();
}

public sealed class BatteryLabel : Widget
{
    public override Element Render()
    {
        var battery = UseObservable(Shell.Power.DisplayDevice);
        var charge = battery.IsPresent ? $"{battery.Percentage:P0}" : "";
        return TextBlock(charge);
    }
}

public sealed class ClockLabel : Component
{
    private readonly ShellPreferences _preferences = new();

    public override Element Render()
    {
        var preferences = UseObservable(_preferences);
        UseEffect(() => () => _preferences.Dispose(), false);
        return TextBlock(preferences.Clock24 ? "13:00" : "1:00 PM");
    }
}

public sealed partial class ShellPreferences : Preferences
{
    [Preference]
    [TomlComment("Show 13:00 rather than 1:00 PM.")]
    public partial bool Clock24 { get; set; }
}

public sealed partial class DockPreferences(string name) : Preferences(name, Check)
{
    [Preference]
    [TomlComment("bottom, left or right.")]
    public partial string Edge { get; set; } = "bottom";

    private static string Check(string key, object value)
    {
        return key == "edge" && value is not ("bottom" or "left" or "right") ? "must be bottom, left or right" : "";
    }
}
