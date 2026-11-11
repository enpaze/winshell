using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static Microsoft.UI.Reactor.Factories;
using static Winshell.Elements;
using Color = global::Windows.UI.Color;

namespace Winshell.ShellTests;

/// <summary>
/// The C# samples in <c>docs/</c> and the README. <see cref="DocsTests"/> requires each <c>csharp</c> fence to appear
/// in this file. Nothing here runs, and none of it opens a window.
/// </summary>
// Guide samples. DocsTests matches their text; the suite does not call them.
public static class GuideSamples
{
    private static IDisposable? _registration;

    /// <summary>Call this from <c>Main</c>, marked <c>[STAThread]</c>.</summary>
    public static void RunTheShell()
    {
        Shell.Run<AppShell>(new ShellOptions { ShellId = "MyShell" });
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
        // The registration stays for the life of the shell. The call below is a docs sample.
        _registration =
            shell.Ipc.Handle("notes", "hello", (string name) => "hi " + name);
        _ = _registration;
    }

    public static PanelWindow AWidgetArea()
    {
        var host = new ReactorHostControl { ComponentFactory = () => new EndArea() };
        var panel = new PanelWindow { Height = 48, Content = host, Visible = true };
        panel.Closed += host.Dispose;
        return panel;
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

    public static PanelWindow ASurface()
    {
        var host = new ReactorHostControl { ComponentFactory = () => new Dock() };
        var dock = new PanelWindow { Anchors = Edges.Bottom, Height = 56, Content = host, Visible = true };
        dock.Closed += host.Dispose;
        return dock;
    }

    public static void AMovedStyle(Shell shell)
    {
        shell.Style = new ShellStyle
        {
            Background = Color.FromArgb(255, 0x1a, 0x1b, 0x26),
            Foreground = Color.FromArgb(255, 0xa9, 0xb1, 0xd6),
            Alert = Color.FromArgb(255, 0xf7, 0x76, 0x8e),
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

    public static void AnAlert(WidgetContext context, TextBlock label)
    {
        if (context.Style is { } style)
            label.Foreground = new SolidColorBrush(style.Alert);
    }

    public static void AHotkey(Shell shell)
    {
        var hotkey = shell.NewHotkey();
        hotkey.Sequence = "Win+E";
        hotkey.Pressed += (_, _) => { };
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
        Surface(WidgetArea("dock", ["start", "tasks"]), new Thickness(4));
}

public sealed class EndArea : Component
{
    public override Element Render() => WidgetArea("end",
    [
        new("message", new Dictionary<string, object> { ["text"] = "Hello" }),
        new("message", new Dictionary<string, object> { ["text"] = "World" })
    ]);
}

public sealed class ClockWidget : Microsoft.UI.Reactor.Core.Component<WidgetContext>
{
    public override Microsoft.UI.Reactor.Core.Element Render() =>
        Microsoft.UI.Reactor.Factories.TextBlock((string)Props.Settings["text"]);
}

public sealed class BatteryLabel : Microsoft.UI.Reactor.Core.Component<WidgetContext>
{
    public override Microsoft.UI.Reactor.Core.Element Render()
    {
        var battery = UseObservable(Props.Shell.Power.DisplayDevice);
        var charge = battery.IsPresent ? $"{battery.Percentage:P0}" : "";
        return Microsoft.UI.Reactor.Factories.TextBlock(charge);
    }
}

public sealed class ClockLabel : Microsoft.UI.Reactor.Core.Component
{
    private readonly ShellPreferences _preferences = new();

    public override Microsoft.UI.Reactor.Core.Element Render()
    {
        var preferences = UseObservable(_preferences);
        UseEffect(() => () => _preferences.Dispose(), false);
        return Microsoft.UI.Reactor.Factories.TextBlock(preferences.Clock24 ? "13:00" : "1:00 PM");
    }
}

/// <summary>The smallest shell: one panel.</summary>
public sealed class AppShell : ShellConfiguration
{
    public override void Start(Shell shell)
    {
        _ = shell;
        GuideSamples.APanel();
    }
}

// The configuration guide's samples: DocsTests matches this text, and the suite never passes them across the WinRT ABI.
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
