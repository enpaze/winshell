using System.ComponentModel;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell;

/// <summary>
/// What a <see cref="WidgetArea"/> shows. <paramref name="Name"/> selects the area's section of the preferences file.
/// That section holds one <c>widgets</c> list, which replaces <paramref name="Defaults"/> while the file has it. An
/// empty name uses only the defaults. The component reads the section when it renders and follows later saves.
/// </summary>
/// <param name="Name">The section, or <c>""</c> to use only <paramref name="Defaults"/>.</param>
/// <param name="Defaults">The author's ordered placements. A missing list is none.</param>
/// <param name="Orientation">The direction the placements run.</param>
/// <param name="Style">This area's own style, which wins over the shell's, or null for the shell's.</param>
public sealed record WidgetAreaProps(
    string Name = "",
    IReadOnlyList<Placement>? Defaults = null,
    Orientation Orientation = Orientation.Horizontal,
    ShellStyle? Style = null);

/// <summary>
/// An ordered area of widgets, in one direction. A bar shows three of these in one tree. The shell's
/// <see cref="Shell.Style"/>, or the area's own, styles the plain controls a widget renders; area-sensitive XAML uses
/// ThemeResource. Unloading the host leaves the widgets mounted; disposing the host unmounts them.
/// </summary>
public sealed class WidgetArea : Component<WidgetAreaProps>
{
    private static readonly IReadOnlyDictionary<string, object> EmptySettings = new Dictionary<string, object>();

    /// <summary>The placements, in order. A widget whose name stays is the same component.</summary>
    public override Element Render()
    {
        var props = Props;
        var defaults = props.Defaults ?? [];
        var root = UseRef<FrameworkElement?>(null);
        var contexts = UseRef(new List<WidgetContext>());
        var names = UseRef<string[]?>(null);
        var generation = UseRef(0);
        var window = UseRef<ShellWindow?>(null);
        var defaultsRef = UseRef<IReadOnlyList<Placement>>(defaults);
        var styleRef = UseRef<ShellStyle?>(props.Style);
        var anchor = UseRef<PopupAnchor?>(null);
        var slot = UseRef<SectionStore?>(null);
        defaultsRef.Current = defaults;
        styleRef.Current = props.Style;
        if (anchor.Current is null)
        {
            var created = new PopupAnchor { Style = () => styleRef.Current ?? Shell.Current.Style };
            anchor.Current = created;
        }

        var (_, bumpWindow) = UseReducer(0);
        var (_, bumpRemake) = UseReducer(0);
        var (_, bumpFile) = UseReducer(0);
        UseEffect(() => () => anchor.Current?.Stop(), false);
        UseEffect(() =>
        {
            if (props.Style is not null)
                return () => { };
            void Follow() => bumpFile(pulse => pulse + 1);
            var shell = Shell.Current;
            shell.StyleChanged += Follow;
            return () => shell.StyleChanged -= Follow;
        }, props.Style);

        if (slot.Current is null || slot.Current.Name != props.Name)
            slot.Current = new SectionStore(props.Name, () => defaultsRef.Current);
        var store = slot.Current;
        store.NoteDefaults();
        UseEffect(() =>
        {
            var mine = store;
            var unsubscribe = mine.Subscribe(() => bumpFile(pulse => pulse + 1));
            if (mine.Refresh())
                bumpFile(pulse => pulse + 1);
            return () =>
            {
                unsubscribe();
                mine.Dispose();
            };
        }, store);

        if (root.Current is { } mounted)
            Restyle(mounted);

        var placements = store.Snapshot;
        var listed = names.Current;
        if (listed is null || listed.Length != placements.Count ||
            !listed.Zip(placements).All(pair => pair.First == pair.Second.Widget))
        {
            names.Current = placements.Select(placement => placement.Widget).ToArray();
            generation.Current++;
        }

        var shown = contexts.Current;
        var slots = new Element[placements.Count];
        for (var i = 0; i < placements.Count; i++)
        {
            var placement = placements[i];
            var settings = placement.Settings ?? EmptySettings;
            WidgetContext context;
            if (i < shown.Count && shown[i].Widget == placement.Widget)
                context = shown[i] = shown[i].Next(settings, props.Orientation).In(window.Current);
            else
            {
                context = new WidgetContext(Shell.Current, settings, props.Orientation, window.Current,
                    anchor.Current!, placement.Widget);
                if (i < shown.Count)
                    shown[i] = context;
                else
                    shown.Add(context);
            }

            slots[i] = Component<Slot, SlotProps>(new SlotProps(context, placement.Widget, props.Name))
                .WithKey($"{i}:{placement.Widget}:{generation.Current}");
        }

        if (shown.Count > placements.Count)
            shown.RemoveRange(placements.Count, shown.Count - placements.Count);

        var stack = props.Orientation == Orientation.Vertical ? VStack(0, slots) : HStack(0, slots);
        return stack.OnMount(element =>
        {
            root.Current = element;
            if (element is FrameworkElement panel)
            {
                MountedHost.Attach(panel, () =>
                {
                    generation.Current++;
                    bumpRemake(pulse => pulse + 1);
                });
                void Note()
                {
                    var found = ShellWindow.Of(panel);
                    if (ReferenceEquals(found, window.Current))
                        return;
                    window.Current = found;
                    bumpWindow(pulse => pulse + 1);
                }

                Note();
                panel.Loaded += (_, _) => Note();
            }

            Restyle(element);
        }).OnUnmount(element =>
        {
            if (element is FrameworkElement panel)
                MountedHost.Detach(panel);
        });

        void Restyle(FrameworkElement element)
        {
            if (StyleHost.Show(element, props.Style) && anchor.Current is { } live)
                live.RaiseStyleChanged();
        }
    }

    private sealed record SlotProps(WidgetContext Context, string Widget, string Area);

    /// <summary>One placement. It stops styling popups when the area drops it, and a render error stays in its place.</summary>
    private sealed class Slot : Component<SlotProps>
    {
        public override Element Render()
        {
            var context = Props.Context;
            var widget = Props.Widget;
            var area = Props.Area;
            UseEffect(() => () => context.StopStyling(), false);

            // The verb stays in the key so a later failure of the other kind mounts this again and logs again.
            Element Failed(string verb, Exception error) =>
                Component<FailedPlacement, Failure>(new Failure(verb, widget, area, error)).WithKey(verb);

            try
            {
                return ErrorBoundary(Widgets.Create(widget, context), error => Failed("render", error));
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                return Failed("create", error);
            }
        }
    }

    private sealed record Failure(string Verb, string Name, string Area, Exception Error);

    /// <summary>The placeholder and the log line for one placement that could not be shown.</summary>
    private sealed class FailedPlacement : Component<Failure>
    {
        public override Element Render()
        {
            var failure = Props;
            UseEffect(() =>
            {
                Shell.Current.Log.Error("widgets",
                    $"Could not {failure.Verb} {failure.Name} in area '{failure.Area}': {failure.Error}");
                return () => { };
            }, false);
            return TextBlock($"{failure.Name}: {failure.Error.Message}");
        }
    }

    /// <summary>The area's section, read while the component renders and released when the name changes or it unmounts.</summary>
    private sealed class SectionStore : IDisposable
    {
        private readonly Func<IReadOnlyList<Placement>> _defaults;
        private readonly AreaPreferences? _section;
        private IReadOnlyList<Placement> _shown;
        private bool _disposed;

        public SectionStore(string name, Func<IReadOnlyList<Placement>> defaults)
        {
            Name = name;
            _defaults = defaults;
            if (name.Length > 0)
                _section = new AreaPreferences(name, defaults);
            _shown = _section?.Placements ?? defaults();
        }

        public string Name { get; }

        public IReadOnlyList<Placement> Snapshot => _shown;

        /// <summary>The author's list, when the file has none. A file list keeps the screen as it is.</summary>
        public void NoteDefaults()
        {
            if (_section?.Placements is not null)
                return;
            var next = _defaults();
            if (!Same(_shown, next))
                _shown = next;
        }

        /// <summary>Reads the section again. Returns whether the list on screen changed.</summary>
        public bool Refresh()
        {
            var next = _section?.Placements ?? _defaults();
            if (Same(_shown, next))
                return false;
            _shown = next;
            return true;
        }

        public Action Subscribe(Action changed)
        {
            if (_section is null)
                return () => { };
            void Handler(object? sender, PropertyChangedEventArgs args)
            {
                if (Refresh())
                    changed();
            }

            _section.PropertyChanged += Handler;
            return () => _section.PropertyChanged -= Handler;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _section?.Dispose();
        }

        private static bool Same(IReadOnlyList<Placement> left, IReadOnlyList<Placement> right)
        {
            if (left.Count != right.Count)
                return false;
            for (var i = 0; i < left.Count; i++)
            {
                if (left[i].Widget != right[i].Widget || !TomlDocument.Same(left[i].Settings, right[i].Settings))
                    return false;
            }

            return true;
        }
    }

    /// <summary>A named area's section: the <c>widgets</c> list that replaces its defaults while the file has it.</summary>
    private sealed class AreaPreferences(string name, Func<IReadOnlyList<Placement>> defaults)
        : Preferences(name, (_, value) => Placement.Check(value))
    {
        [TomlComment(
            "The widgets, in order: each table names one and holds its settings. The list replaces the shell's whole\n" +
            "default list, and widgets = [] leaves the area empty. Comment it out to use the shell's defaults again.")]
        public IReadOnlyList<IReadOnlyDictionary<string, object?>>? Widgets
        {
            get;
            set => Set(ref field, value);
        }

        /// <summary>The list as placements, or null when the file has none.</summary>
        public IReadOnlyList<Placement>? Placements => Widgets is { } rows ? Placement.Parse(rows) : null;

        /// <summary>A missing file shows the area's defaults, which are what the list replaces.</summary>
        internal override object? Commented(TomlKey key) => Placement.Format(defaults());
    }
}
