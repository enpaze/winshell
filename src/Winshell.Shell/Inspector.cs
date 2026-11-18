using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Reactor.Core.Diagnostics;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;

namespace Winshell;

internal static class Inspector
{
    private const byte DarkBack = 32;
    private const byte LightBack = 243;

    public static async Task<string> Run(Shell shell, string command, IReadOnlyList<string> args)
    {
        var window = args.Skip(1).ToList();
        return command switch
        {
            "tree" => Tree(shell),
            "screenshot" when args.Count > 0 => await Screenshot(Shown(shell, window), args[0], shell.IsDark),
            "click" when args.Count > 0 => Click(Find(shell, command, args[0], window), args[0]),
            "type" when args.Count > 1 => Type(Find(shell, command, args[0], (string[])[.. args.Skip(2)]), args[0],
                args[1]),
            "hover" when args.Count > 0 => Hover(Find(shell, command, args[0], window), args[0]),
            "record" => RecordTarget(shell, args),
            "focus" when args.Count > 0 => await shell.Input.Focus(Find(shell, command, args[0], window), args[0]),
            "point" when args.Count > 0 => await shell.Input.Point(Find(shell, command, args[0], window), args[0]),
            "leave" => await shell.Input.Leave(),
            "press" when args is ["--right", var name, ..] =>
                await shell.Input.Press(Find(shell, command, name, (string[])[.. args.Skip(2)]), name, true),
            "press" when args.Count > 0 => await shell.Input.Press(Find(shell, command, args[0], window), args[0],
                false),
            "scroll" when args.Count > 1 && int.TryParse(args[1], NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out var steps) && steps != 0 =>
                await shell.Input.Scroll(Find(shell, command, args[0], (string[])[.. args.Skip(2)]), args[0], steps),
            "key" when args.Count > 0 => await shell.Input.Key(Window(shell, window), args[0]),
            _ => throw new IpcException($"Unknown inspect command {command}.")
        };
    }

    internal static byte[] Flattened(byte[] bgra, byte back)
    {
        for (var i = 0; i < bgra.Length; i += 4)
        {
            var through = 255 - bgra[i + 3];
            for (var channel = i; channel < i + 3; channel++)
                bgra[channel] = (byte)Math.Min(255, bgra[channel] + back * through / 255);
            bgra[i + 3] = 255;
        }

        return bgra;
    }

    private static List<ShellWindow> Windows(Shell shell)
    {
        return [.. shell.Platform.Windows.Where(host => host.Place.IsVisible).Select(host => host.Client)];
    }

    private static string Tree(Shell shell)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartArray();
            var windows = Windows(shell);
            for (var i = 0; i < windows.Count; i++)
            {
                var window = windows[i];
                json.WriteStartObject();
                json.WriteNumber("window", i);
                json.WriteString("kind", window.GetType().Name);
                json.WriteNumber("x", window.Origin.X);
                json.WriteNumber("y", window.Origin.Y);
                json.WriteNumber("width", window.ActualWidth);
                json.WriteNumber("height", window.ActualHeight);
                if (window.Content is { } content)
                {
                    var focused = content.XamlRoot is { } xamlRoot ? FocusManager.GetFocusedElement(xamlRoot) : null;
                    json.WritePropertyName("content");
                    Write(json, content, content, focused);
                    if (Popups(content) is { Count: > 0 } popups)
                    {
                        json.WriteStartArray("popups");
                        foreach (var popup in popups)
                            Write(json, popup, popup, focused);
                        json.WriteEndArray();
                    }
                }

                json.WriteEndObject();
            }

            json.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Write(Utf8JsonWriter json, DependencyObject element, UIElement root, object? focused)
    {
        json.WriteStartObject();
        json.WriteString("type", element is UIElement visual ? ContentName(visual) : element.GetType().Name);
        if (element is FrameworkElement framed)
        {
            if (framed.Name.Length > 0)
                json.WriteString("name", framed.Name);
            if (AutomationProperties.GetAutomationId(framed) is { Length: > 0 } id)
                json.WriteString("automationId", id);
            if (AutomationProperties.GetName(framed) is { Length: > 0 } label)
                json.WriteString("label", label);
            var at = framed.TransformToVisual(root).TransformPoint(new Point(0, 0));
            json.WriteNumber("x", at.X);
            json.WriteNumber("y", at.Y);
            json.WriteNumber("width", framed.ActualWidth);
            json.WriteNumber("height", framed.ActualHeight);
            if (framed.Visibility != Visibility.Visible)
                json.WriteBoolean("visible", false);
        }

        if (ReferenceEquals(element, focused))
            json.WriteBoolean("focused", true);
        Values(json, element);
        var count = VisualTreeHelper.GetChildrenCount(element);
        if (count > 0)
        {
            json.WriteStartArray("children");
            for (var i = 0; i < count; i++)
                Write(json, VisualTreeHelper.GetChild(element, i), root, focused);
            json.WriteEndArray();
        }

        json.WriteEndObject();
    }

    private static void Values(Utf8JsonWriter json, DependencyObject element)
    {
        switch (element)
        {
            case TextBlock { Text: { Length: > 0 } text }:
                json.WriteString("text", text);
                break;
            case TextBox box:
                json.WriteString("text", box.Text);
                break;
            case RangeBase range:
                json.WriteNumber("value", range.Value);
                break;
            case ToggleSwitch toggle:
                json.WriteBoolean("on", toggle.IsOn);
                break;
            case Selector selector:
                json.WriteNumber("selectedIndex", selector.SelectedIndex);
                break;
        }

        if (element is ToggleButton { IsChecked: { } isChecked })
            json.WriteBoolean("checked", isChecked);
        if (element is ContentControl { Content: string content })
            json.WriteString("text", content);
        if (element is Control { IsEnabled: false })
            json.WriteBoolean("enabled", false);
    }

    private static ShellWindow Window(Shell shell, IReadOnlyList<string> named)
    {
        var windows = Windows(shell);
        if (windows.Count == 0)
            throw new IpcException("No window is open.");
        if (named is not [var name, ..])
            return windows[0];
        if (int.TryParse(name, out var index) && index >= 0 && index < windows.Count)
            return windows[index];
        return windows.FirstOrDefault(window => window.Content is { } content && ContentName(content) == name)
               ?? throw new IpcException($"No open window shows {name}.");
    }

    private static FrameworkElement Find(Shell shell, string command, string name, IReadOnlyList<string> window)
    {
        var windows = Windows(shell);
        var searched = window.Count > 0 ? [Window(shell, window)] : windows;
        var found = new List<(int Window, FrameworkElement Element)>();
        foreach (var open in searched)
        {
            if (open.Content is not { } content)
                continue;
            var at = windows.IndexOf(open);
            foreach (var root in (UIElement[])[content, .. Popups(content)])
                Find(root, name, element => found.Add((at, element)));
        }

        return found switch
        {
            [] => throw new IpcException($"No element is named {name}. winshell inspect tree lists them."),
            [var (_, element)] => element,
            _ => throw new IpcException(Ambiguous(command, name, found, windows))
        };
    }

    private static string Ambiguous(string command, string name, List<(int Window, FrameworkElement Element)> found,
        List<ShellWindow> windows)
    {
        var quoted = name.Any(char.IsWhiteSpace) ? $"\"{name}\"" : name;
        var hint = found.DistinctBy(match => match.Window).Count() > 1
            ? $"Add the window, as in winshell inspect {command} {quoted} {found[0].Window}, or use a name only one of them has."
            : "Use a name only one of them has.";
        return string.Join('\n', [
            $"{name} names {found.Count} elements:",
            .. found.Select(match =>
                $"  window {match.Window} ({ShownName(windows[match.Window])}): {Described(match.Element)}"),
            hint
        ]);
    }

    private static string ContentName(UIElement content)
    {
        if (content is ReactorHostControl host)
        {
            var component = ReactorDiagnostics.GetHosts()
                .FirstOrDefault(info => ReferenceEquals(info.HostControl, host))
                ?.RootComponentName;
            if (component is not null)
                return ClassName(component);
        }

        return content.GetType().Name;
    }

    private static string ShownName(ShellWindow window)
    {
        return window.Content is { } content ? ContentName(content) : "";
    }

    private static string ClassName(string qualified)
    {
        var open = qualified.IndexOf('<');
        var bare = open < 0 ? qualified : qualified[..open];
        var cut = Math.Max(bare.LastIndexOf('.'), bare.LastIndexOf('+'));
        var name = cut < 0 ? bare : bare[(cut + 1)..];
        return open < 0 ? name : name + qualified[open..];
    }

    private static string Described(FrameworkElement element)
    {
        var text = element.GetType().Name;
        if (element.Name.Length > 0)
            text += $" name={element.Name}";
        if (AutomationProperties.GetAutomationId(element) is { Length: > 0 } id)
            text += $" id={id}";
        if (AutomationProperties.GetName(element) is { Length: > 0 } label)
            text += $" label=\"{label}\"";
        return text;
    }

    private static List<UIElement> Popups(UIElement content)
    {
        if (content.XamlRoot is not { } root)
            return [];
        var open = new List<UIElement>();
        foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(root))
            if (popup.Child is { } child)
                open.Add(child);
        return open;
    }

    private static UIElement Shown(Shell shell, IReadOnlyList<string> named)
    {
        if (named is [var name, ..] && Windows(shell).Select(open => open.Content).OfType<UIElement>()
                .SelectMany(Popups)
                .FirstOrDefault(popup => popup.GetType().Name == name) is { } shown)
            return shown;
        return Window(shell, named).Content?.XamlRoot?.Content ?? throw new IpcException("That window is empty.");
    }

    private static void Find(DependencyObject element, string name, Action<FrameworkElement> found)
    {
        if (element is UIElement { Visibility: Visibility.Collapsed })
            return;
        if (element is FrameworkElement framed
            && (framed.Name == name || AutomationProperties.GetAutomationId(framed) == name ||
                AutomationProperties.GetName(framed) == name))
        {
            found(framed);
            return;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            Find(VisualTreeHelper.GetChild(element, i), name, found);
    }

    private static string Click(FrameworkElement element, string name)
    {
        ShellWindow.Of(element)?.Host?.Messages.DismissNativeMenus();
        var peer = FrameworkElementAutomationPeer.FromElement(element) ??
                   FrameworkElementAutomationPeer.CreatePeerForElement(element);
        switch (peer?.GetPattern(PatternInterface.Invoke), peer?.GetPattern(PatternInterface.Toggle),
            peer?.GetPattern(PatternInterface.ExpandCollapse))
        {
            case (IInvokeProvider invoke, _, _):
                invoke.Invoke();
                break;
            case (_, IToggleProvider toggle, _):
                toggle.Toggle();
                break;
            case (_, _, IExpandCollapseProvider expand):
                expand.Expand();
                break;
            default:
                throw new IpcException($"{name} is a {element.GetType().Name}, which takes no click.");
        }

        return $"Clicked {name}";
    }

    private static string Type(FrameworkElement element, string name, string text)
    {
        if (element is not TextBox box)
            throw new IpcException($"{name} is a {element.GetType().Name}, which takes no text.");
        if (box.IsReadOnly || !box.IsEnabled)
            throw new IpcException($"{name} is {(box.IsReadOnly ? "read-only" : "disabled")}, so it takes no text.");
        box.Text = text;
        return $"Typed into {name}";
    }

    private static string Hover(FrameworkElement element, string name)
    {
        if (element is Control control && VisualStateManager.GoToState(control, "PointerOver", false))
            return $"Hovering {name}";
        throw new IpcException($"{name} is a {element.GetType().Name}, which has no hover look.");
    }

    private static string RecordTarget(Shell shell, IReadOnlyList<string> args)
    {
        var screens = shell.Screens;
        if (screens.Count == 0)
            throw new IpcException("No screen is connected.");
        nint handle = 0;
        ScreenInfo screen;
        if (args is ["--screen", var number])
        {
            if (!int.TryParse(number, out var index) || index < 0 || index >= screens.Count)
                throw new IpcException(
                    $"There is no screen {number}. Screens are numbered from 0, the primary one first, and there are {screens.Count}.");
            screen = screens[index];
        }
        else
        {
            handle = Window(shell, args).Host?.Handle ?? 0;
            if (handle == 0)
                throw new IpcException("That window has no native window to record.");
            var on = Screens.Of(handle)?.Id;
            screen = screens.FirstOrDefault(candidate => candidate.Id == on) ?? screens[0];
        }

        return new InspectRecordTarget((long)handle, screen.Id, screen.RefreshRate).Write();
    }

    private static async Task<string> Screenshot(UIElement shown, string file, bool dark)
    {
        if (shown is ToolTip && VisualTreeHelper.GetChildrenCount(shown) > 0 &&
            VisualTreeHelper.GetChild(shown, 0) is UIElement root)
            shown = root;
        var clear = shown is Panel { Background: null } panel ? panel : null;
        var bitmap = new RenderTargetBitmap();
        try
        {
            clear?.Background = new SolidColorBrush(Colors.Transparent);
            await bitmap.RenderAsync(shown);
        }
        finally
        {
            clear?.Background = null;
        }

        var pixels = await bitmap.GetPixelsAsync();
        var image = new PixelImage(bitmap.PixelWidth, bitmap.PixelHeight,
            Flattened(pixels.ToArray(), dark ? DarkBack : LightBack));
        await File.WriteAllBytesAsync(file, image.EncodePng());
        return $"{bitmap.PixelWidth}x{bitmap.PixelHeight} {file}";
    }
}
