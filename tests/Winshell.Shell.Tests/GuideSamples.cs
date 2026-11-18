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
        Shell.Run(shell => _ = new Bar { Center = ["clock"] });
    }

    public static void APanel()
    {
        _ = new PanelWindow
        {
            Anchors = Edges.Left | Edges.Right | Edges.Bottom,
            Height = 48,
            ReservedSize = 48,
            Content = new TextBlock { Text = "Winshell" },
            Visible = true
        };
    }

    public static ScreenWindows ADockOnEachScreen()
    {
        var dock = new ScreenWindows("dock", (screen, row) => new PanelWindow
        {
            Screen = screen,
            Anchors = row?.GetValueOrDefault("edge") is "top"
                ? Edges.Left | Edges.Right | Edges.Top
                : Edges.Left | Edges.Right | Edges.Bottom,
            Height = 48,
            ReservedSize = 48,
            Content = new TextBlock { Text = screen.Name },
            Visible = true
        });
        return dock;
    }

    public static void ANote(Shell shell)
    {
        _registration =
            shell.Ipc.Handle("notes", "hello", (string name) => "hi " + name);
        _ = _registration;
    }

    public static PanelWindow AWidgetArea()
    {
        var panel = new PanelWindow<EndArea> { Height = 48, Visible = true };
        return panel;
    }

    public static (PopupWindow Calendar, Input.Hotkey Hotkey) AComponentPopup(Shell shell)
    {
        var calendar = new PopupWindow<Calendar> { Position = PopupPosition.Top, Width = 320, Height = 240 };
        var hotkey = shell.Hotkey("Win+Alt+C", calendar.Toggle);
        return (calendar, hotkey);
    }

    public static (PopupWindow Today, PopupWindow Later) TwoNotes()
    {
        var today = new PopupWindow<Note, string>("Stand-up at 10") { Position = PopupPosition.Top };
        var later = new PopupWindow<Note, string>("Pay the rent");
        return (today, later);
    }

    public static (Bar Top, Bar Side) Bars()
    {
        var bar = new Bar
        {
            Edge = Edges.Top,
            Start = ["workspaces"],
            Center = ["clock"],
            End = ["tray", new("message", new Dictionary<string, object> { ["text"] = "Hello" })]
        };
        var side = new Bar { Name = "side", Edge = Edges.Left, Reserve = false, Start = ["clock"] };
        return (bar, side);
    }

    public static Launcher ALauncher(Shell shell)
    {
        var launcher = new Launcher
        {
            Hotkey = "Alt+Space",
            Commands =
            [
                new("Lock", () => shell.Session.Lock()),
                new("Reload shell", () => shell.Reload())
            ]
        };
        return launcher;
    }

    public static PanelWindow ASurface()
    {
        var dock = new PanelWindow<Dock> { Anchors = Edges.Bottom, Height = 56, Visible = true };
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

    public static void RegisterAComponentWidget()
    {
        Widgets.Register<ClockWidget>("clock");
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

public sealed class ClockWidget : Widget
{
    public override Element Render() =>
        TextBlock(Setting("text", ""));
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

#pragma warning disable CsWinRT1028
public sealed class ShellPreferences : Preferences
{
    [TomlComment("Show 13:00 rather than 1:00 PM.")]
    public bool Clock24
    {
        get => Read(field);
        set => Set(ref field, value);
    }
}

public sealed class DockPreferences(string name) : Preferences(name, Check)
{
    [TomlComment("bottom, left or right.")]
    public string Edge
    {
        get => Read(field);
        set => Set(ref field, value);
    } = "bottom";

    private static string Check(string key, object value)
    {
        return key == "edge" && value is not ("bottom" or "left" or "right") ? "must be bottom, left or right" : "";
    }
}
#pragma warning restore CsWinRT1028
