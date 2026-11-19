using System.Collections.Immutable;
using System.ComponentModel;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell;

public sealed record WidgetAreaProps(
    string Name = "",
    ImmutableArray<Placement>? Defaults = null,
    Orientation Orientation = Orientation.Horizontal,
    ShellStyle? Style = null);

public sealed class WidgetArea : Component<WidgetAreaProps>
{
    private static readonly IReadOnlyDictionary<string, object> EmptySettings = new Dictionary<string, object>();

    public override Element Render()
    {
        var props = Props;
        var defaults = props.Defaults ?? [];
        var root = UseRef<FrameworkElement?>(null);
        var contexts = UseRef(new List<WidgetContext>());
        var names = UseRef<string[]?>(null);
        var generation = UseRef(0);
        var window = UseRef<ShellWindow?>(null);
        var defaultsRef = UseRef<ImmutableArray<Placement>>(defaults);
        var styleRef = UseRef<ShellStyle?>(props.Style);
        var scopeRef = UseRef<StyleScope?>(null);
        var slot = UseRef<SectionStore?>(null);
        defaultsRef.Current = defaults;
        styleRef.Current = props.Style;
        var scope = scopeRef.Current ??= new StyleScope { Style = () => styleRef.Current ?? Shell.Current.Style };

        var (_, bumpWindow) = UseReducer(0);
        var (_, bumpRemake) = UseReducer(0);
        var (_, bumpFile) = UseReducer(0);
        UseEffect(() => () => scope.Stop(), false);
        UseEffect(StyleHost.FollowShellStyle(props.Style, () => bumpFile(pulse => pulse + 1)), props.Style);

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
        if (listed is null || listed.Length != placements.Length ||
            !listed.Zip(placements).All(pair => pair.First == pair.Second.Widget))
        {
            names.Current = placements.Select(placement => placement.Widget).ToArray();
            generation.Current++;
        }

        var shown = contexts.Current;
        var slots = new Element[placements.Length];
        for (var i = 0; i < placements.Length; i++)
        {
            var placement = placements[i];
            var settings = placement.Settings ?? EmptySettings;
            WidgetContext context;
            if (i < shown.Count && shown[i].Widget == placement.Widget)
                context = shown[i] = shown[i] with { Settings = settings, Orientation = props.Orientation, Window = window.Current };
            else
            {
                context = new WidgetContext(Shell.Current, settings, props.Orientation, window.Current,
                    scope, placement.Widget);
                if (i < shown.Count)
                    shown[i] = context;
                else
                    shown.Add(context);
            }

            slots[i] = Component<Slot, SlotProps>(new SlotProps(context, placement.Widget, props.Name))
                .WithKey($"{i}:{placement.Widget}:{generation.Current}");
        }

        if (shown.Count > placements.Length)
            shown.RemoveRange(placements.Length, shown.Count - placements.Length);

        var stack = props.Orientation == Orientation.Vertical ? VStack(0, slots) : HStack(0, slots);
        return stack.Provide(StyleHost.InEffect, scope).OnMount(element =>
        {
            root.Current = element;
            if (element is FrameworkElement panel)
            {
                HotReload.Register(panel, () =>
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
        });

        void Restyle(FrameworkElement element)
        {
            if (StyleHost.Show(element, props.Style))
                scope.RaiseStyleChanged();
        }
    }

    private sealed record SlotProps(WidgetContext Context, string Widget, string Area);

    private sealed class Slot : Component<SlotProps>
    {
        public override Element Render()
        {
            var context = Props.Context;
            var widget = Props.Widget;
            var area = Props.Area;
            UseEffect(() => () => context.StopStyling(), false);
            try
            {
                return ErrorBoundary(
                    context.Shell.WidgetRegistry.Create(widget, context),
                    error => Component<FailedPlacement, Failure>(new Failure("render", widget, area, error))
                        .WithKey("render"));
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                return Component<FailedPlacement, Failure>(new Failure("create", widget, area, error)).WithKey("create");
            }
        }
    }

    private sealed record Failure(string Verb, string Name, string Area, Exception Error);

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

    private sealed class SectionStore : IDisposable
    {
        private readonly Func<ImmutableArray<Placement>> _defaults;
        private readonly AreaPreferences? _section;
        private ImmutableArray<Placement> _shown;
        private bool _disposed;

        public SectionStore(string name, Func<ImmutableArray<Placement>> defaults)
        {
            Name = name;
            _defaults = defaults;
            if (name.Length > 0)
                _section = new AreaPreferences(name, defaults);
            _shown = _section?.Placements ?? defaults();
        }

        public string Name { get; }

        public ImmutableArray<Placement> Snapshot => _shown;

        public void NoteDefaults()
        {
            if (_section?.Placements is not null)
                return;
            var next = _defaults();
            if (!Same(_shown, next))
                _shown = next;
        }

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

        private static bool Same(ImmutableArray<Placement> left, ImmutableArray<Placement> right)
        {
            if (left.Length != right.Length)
                return false;
            for (var i = 0; i < left.Length; i++)
            {
                if (left[i].Widget != right[i].Widget || !TomlDocument.Same(left[i].Settings, right[i].Settings))
                    return false;
            }

            return true;
        }
    }

    private sealed class AreaPreferences(string name, Func<ImmutableArray<Placement>> defaults)
        : Preferences(name, (_, value) => Placement.Check(value))
    {
        [TomlComment(
            "The widgets, in order: a widget's name, or a table that names one and holds its settings. The list\n" +
            "replaces the shell's whole default list, and widgets = [] leaves the area empty. Comment it out to use the\n" +
            "shell's defaults again.")]
        public object? Widgets
        {
            get;
            set => Set(ref field, value);
        }

        public ImmutableArray<Placement>? Placements => Widgets is { } rows ? Placement.Parse(rows) : null;

        internal override object? Commented(TomlKey key) => Placement.Format(defaults());
    }
}
