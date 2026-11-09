using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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

    public static (WidgetArea Area, PanelWindow Panel) AWidgetArea()
    {
        var area = new WidgetArea
        {
            Name = "end",
            Orientation = Orientation.Horizontal,
            Defaults =
            [
                new("message", new Dictionary<string, object> { ["text"] = "Hello" }),
                new("message", new Dictionary<string, object> { ["text"] = "World" })
            ]
        };
        var panel = new PanelWindow { Height = 48, Content = area, Visible = true };
        panel.Closed += area.Dispose;
        return (area, panel);
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
        var area = new WidgetArea { Defaults = ["start", "tasks"] };
        var surface = new Surface { Padding = new Thickness(4), Content = area };
        var dock = new PanelWindow { Anchors = Edges.Bottom, Height = 56, Content = surface, Visible = true };
        dock.Closed += () =>
        {
            area.Dispose();
            surface.Dispose();
        };
        return dock;
    }

    public static void AStyle(Shell shell)
    {
        shell.Style = new Uri("ms-appx:///TokyoNight.xaml");
    }

    public static void AHotkey(Shell shell)
    {
        var hotkey = shell.NewHotkey();
        hotkey.Sequence = "Win+E";
        hotkey.Pressed += (_, _) => { };
    }

    public static TextBlock ABatteryLabel(Shell shell)
    {
        var label = new TextBlock();
        label.Watch(() =>
        {
            var battery = shell.Power.DisplayDevice;
            label.Text = battery.IsPresent ? $"{battery.Percentage:P0}" : "";
        });
        return label;
    }

    public static (TextBlock Clock, DockPreferences Dock) AClock()
    {
        var preferences = new ShellPreferences();
        var clock = new TextBlock();
        clock.Watch(() => clock.Text = preferences.Clock24 ? "13:00" : "1:00 PM");
        var dock = new DockPreferences("dock");
        return (clock, dock);
    }

    public static void Reload(Shell shell)
    {
        if (!shell.Reload())
            shell.Log.Warn("shell", "Reload did not start.");
    }
}

public sealed partial class MessageWidget : UserControl
{
    public MessageWidget(WidgetContext context)
    {
        Content = new TextBlock { Text = (string)context.Settings["text"] };
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
