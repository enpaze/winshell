using System.Collections.Immutable;
using System.Globalization;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using Winshell.Apps;
using Winshell.Input;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell;

public sealed partial class Launcher : ObservableObject, IShellPart
{
    private const double Width = 640;
    private const double Height = 420;

    private readonly Shell _shell = Shell.Current;
    private Hotkey? _hotkey;
    private LauncherPreferences? _preferences;
    private PopupWindow? _window;
    private ReactorHostControl? _host;
    private bool _visible;
    private int _opened;
    private bool _disposed;

    public string Name
    {
        get;
        init => field = string.IsNullOrEmpty(value)
            ? throw new ArgumentException("A launcher needs a name.", nameof(value))
            : value;
    } = "launcher";

    public string Hotkey { get; init; } = "Alt+Space";

    public ImmutableArray<LauncherCommand> Commands { get; init; } = [];

    public bool Visible => Read(_visible);

    public void Show()
    {
        if (_disposed || _visible || _preferences is null)
            return;
        var window = _window ??= MakeWindow();
        _opened++;
        _host!.Props = new LauncherViewProps(_opened, Commands, Run, Hide);
        window.Visible = true;
        window.Focus();
        Set(ref _visible, true, nameof(Visible));
    }

    public void Hide()
    {
        if (!_visible)
            return;
        _window!.Visible = false;
        Set(ref _visible, false, nameof(Visible));
    }

    public void Toggle()
    {
        if (_visible)
            Hide();
        else
            Show();
    }

    void IShellPart.Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_preferences is not null)
            return;
        _preferences = new LauncherPreferences(this);
        _hotkey = _shell.Hotkey(_preferences.Hotkey, Toggle);
        _preferences.PropertyChanged += (_, _) => _hotkey.Sequence = _preferences.Hotkey;
    }

    private PopupWindow MakeWindow()
    {
        _host = new ReactorHostControl
        {
            ComponentFactory = () => new LauncherView(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            IsTabStop = false
        };
        var window = _shell.Add(new PopupWindow
        {
            Position = PopupPosition.Top,
            Width = Width,
            Height = Height,
            GrabFocus = true,
            Corners = WindowCorners.Round,
            Content = _host
        });
        window.Dismissed += Hide;
        return window;
    }

    private void Run(LauncherItem item)
    {
        Hide();
        try
        {
            item.Run();
        }
        catch (Exception e)
        {
            _shell.Log.Warn(Name, $"Could not run {item.Name}: {e.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _shell.Remove(this);
        _hotkey?.Dispose();
        _preferences?.Dispose();
        _host?.Dispose();
        _window?.Dispose();
    }

    private sealed partial class LauncherPreferences(Launcher launcher) : Preferences(launcher.Name)
    {
        [Preference]
        [TomlComment("The hotkey that opens and closes the launcher, such as Alt+Space or Ctrl+Shift+K.")]
        public partial string Hotkey { get; set; } = launcher.Hotkey;
    }
}

internal sealed record LauncherItem(string Key, string Name, string Kind, Action Run);

internal static class LauncherRanking
{
    public const int Shown = 8;

    public static List<LauncherItem> Rank(string query, IReadOnlyList<LauncherItem> apps,
        IReadOnlyList<LauncherItem> commands)
    {
        var text = query.Trim();
        var matching = text.Length == 0
            ? [.. commands]
            : commands.Select(command => (Command: command, Score: AppsService.Score(text, command.Name)))
                .Where(pair => pair.Score > 0)
                .OrderByDescending(pair => pair.Score)
                .Select(pair => pair.Command)
                .ToList();
        var ranked = new List<LauncherItem>(apps.Count + matching.Count);
        var next = 0;
        foreach (var app in apps)
        {
            var score = AppsService.Score(text, app.Name);
            while (next < matching.Count && AppsService.Score(text, matching[next].Name) > score)
                ranked.Add(matching[next++]);
            ranked.Add(app);
        }

        ranked.AddRange(matching.Skip(next));
        return ranked;
    }
}

internal sealed record LauncherViewProps(
    int Opened,
    ImmutableArray<LauncherCommand> Commands,
    Action<LauncherItem> Run,
    Action Hide);

internal sealed class LauncherView : Component<LauncherViewProps>
{
    public override Element Render()
    {
        return Elements.Surface(
            Component<LauncherSearch, LauncherViewProps>(Props)
                .WithKey(Props.Opened.ToString(CultureInfo.InvariantCulture)),
            new Thickness(8));
    }
}

internal sealed class LauncherSearch : Component<LauncherViewProps>
{
    public override Element Render()
    {
        var shell = Shell.Current;
        UseObservableTree(shell.Apps);
        var (query, setQuery) = UseState("");
        var (selected, setSelected) = UseState(0);
        var apps = query.Trim().Length == 0 ? shell.Apps.Recent.ToList() : shell.Apps.Search(query);
        var items = LauncherRanking.Rank(query,
                apps.Select(app => new LauncherItem("app:" + app.Id, app.Name, "App", app.Launch)).ToList(),
                Props.Commands.Select(command =>
                    new LauncherItem("command:" + command.Name, command.Name, "Command", command.Run)).ToList())
            .Take(LauncherRanking.Shown)
            .ToList();
        var at = items.Count == 0 ? 0 : Math.Clamp(selected, 0, items.Count - 1);

        Element Row(LauncherItem item, int index)
        {
            var row = Button(Grid(
                        [GridSize.Star(), GridSize.Auto],
                        [GridSize.Auto],
                        Body(item.Name).TextTrimming(TextTrimming.CharacterEllipsis).Grid(0, 0),
                        Caption(item.Kind).Opacity(0.7).VAlign(VerticalAlignment.Center).Grid(0, 1)),
                    () => Props.Run(item))
                .HAlign(HorizontalAlignment.Stretch)
                .HorizontalContentAlignment(HorizontalAlignment.Stretch)
                .Padding(12, 8, 12, 8)
                .Background(Theme.Ref(index == at ? "SubtleFillColorSecondaryBrush" : "SubtleFillColorTransparentBrush"))
                .BorderThickness(0)
                .AutomationName(item.Name)
                .WithKey(item.Key);
            return index == at ? row.AutomationId("launcher.selected") : row;
        }

        return VStack(8,
                TextBox(query, text =>
                    {
                        setQuery(text);
                        setSelected(0);
                    }, "Search apps and commands")
                    .AutomationId("launcher.query")
                    .AutomationName("Search apps and commands")
                    .OnMount(element => ((Control)element).Focus(FocusState.Programmatic)),
                VStack(2, [.. items.Select(Row)]).AutomationId("launcher.results"))
            .OnPreviewKeyDown((_, e) =>
            {
                switch (e.Key)
                {
                    case VirtualKey.Down:
                        setSelected(Math.Min(at + 1, Math.Max(items.Count - 1, 0)));
                        break;
                    case VirtualKey.Up:
                        setSelected(Math.Max(at - 1, 0));
                        break;
                    case VirtualKey.Enter when items.Count > 0:
                        Props.Run(items[at]);
                        break;
                    case VirtualKey.Escape:
                        Props.Hide();
                        break;
                    default:
                        return;
                }

                e.Handled = true;
            });
    }
}
