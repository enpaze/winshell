using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Winshell;

/// <summary>
/// What one mounted <see cref="ReactorHostControl"/> has to put back after an edit: the widget areas in its tree, and
/// the place a configuration control in that tree goes when it is built again. The host records both. Disposing the
/// host drops them with it. Mounting and <see cref="PanelReload"/> both run on the shell's loop.
/// </summary>
internal sealed class MountedHost
{
    private static readonly ConditionalWeakTable<ReactorHostControl, MountedHost> Hosts = new();

    /// <summary>The host an area panel belongs to. Detach reads it, because the panel may already have left the tree.</summary>
    private static readonly DependencyProperty AreaHostProperty = DependencyProperty.RegisterAttached(
        "AreaHost",
        typeof(ReactorHostControl),
        typeof(MountedHost),
        new PropertyMetadata(null));

    /// <summary>A remake recorded before the panel's host is in the tree. The host takes it on load.</summary>
    private static readonly DependencyProperty PendingProperty = DependencyProperty.RegisterAttached(
        "PendingRemake",
        typeof(Pending),
        typeof(MountedHost),
        new PropertyMetadata(null));

    private readonly ConditionalWeakTable<FrameworkElement, Action> _areas = new();
    private readonly Dictionary<UIElement, Action<UIElement>> _places = [];

    private MountedHost()
    {
    }

    /// <summary>The area <paramref name="panel"/> shows will make its widgets again when an edit touches them.</summary>
    public static void Attach(FrameworkElement panel, Action remake)
    {
        panel.Loaded -= OnLoaded;
        panel.SetValue(PendingProperty, new Pending(remake));
        if (HostOf(panel) is { } host)
        {
            Bind(host, panel, remake);
            return;
        }

        // Loaded does not bubble, and subscribing after it has run does not replay it, so the host is claimed above
        // when the panel is already in the tree.
        panel.Loaded += OnLoaded;
    }

    /// <summary>The area left the tree. A later edit must not make its widgets again.</summary>
    public static void Detach(FrameworkElement panel)
    {
        panel.Loaded -= OnLoaded;
        panel.ClearValue(PendingProperty);
        if (panel.GetValue(AreaHostProperty) is not ReactorHostControl host)
            return;
        panel.ClearValue(AreaHostProperty);
        if (Hosts.TryGetValue(host, out var mounted))
            mounted._areas.Remove(panel);
    }

    /// <summary>Whether <paramref name="panel"/> is a widget area a mounted host recorded.</summary>
    public static bool IsArea(FrameworkElement panel)
    {
        if (Claim(panel) is { } mounted && mounted._areas.TryGetValue(panel, out _))
            return true;
        return panel.GetValue(PendingProperty) is Pending;
    }

    /// <summary>
    /// The remake for a widget area at <paramref name="element"/>, when a mounted host recorded one. An area absorbs an
    /// edit inside it, so the surrounding view stays.
    /// </summary>
    public static bool TryRemake(DependencyObject element, out Action remake)
    {
        if (element is FrameworkElement panel && Claim(panel) is { } mounted &&
            mounted._areas.TryGetValue(panel, out var found))
        {
            remake = found;
            return true;
        }

        remake = null!;
        return false;
    }

    /// <summary>Records where <paramref name="control"/> goes, on the host that shows it, before a rebuild puts a new one there.</summary>
    public static void Remember(UIElement control)
    {
        if (HostOf(control) is not { } host || Slot(control) is not { } place)
            return;
        Of(host)._places[control] = place;
    }

    /// <summary>
    /// Where a rebuilt <paramref name="control"/> goes. The host's record wins. A control outside every host still uses
    /// the slot its parent exposes. The window's own content is not this slot.
    /// </summary>
    public static Action<UIElement>? TakePlace(UIElement control)
    {
        if (HostOf(control) is { } host && Hosts.TryGetValue(host, out var mounted) &&
            mounted._places.Remove(control, out var recorded))
            return recorded;
        return Slot(control);
    }

    /// <summary>Drops a place <see cref="Remember"/> kept, including when the rebuild did not use it.</summary>
    public static void Forget(UIElement control)
    {
        if (HostOf(control) is { } host && Hosts.TryGetValue(host, out var mounted))
            mounted._places.Remove(control);
    }

    private static void OnLoaded(object sender, RoutedEventArgs args)
    {
        var panel = (FrameworkElement)sender;
        panel.Loaded -= OnLoaded;
        if (panel.GetValue(PendingProperty) is Pending pending && HostOf(panel) is { } host)
            Bind(host, panel, pending.Remake);
    }

    private static void Bind(ReactorHostControl host, FrameworkElement panel, Action remake)
    {
        var mounted = Of(host);
        mounted._areas.Remove(panel);
        mounted._areas.Add(panel, remake);
        panel.SetValue(AreaHostProperty, host);
        panel.ClearValue(PendingProperty);
    }

    private static MountedHost? Claim(FrameworkElement panel)
    {
        if (panel.GetValue(PendingProperty) is Pending pending && HostOf(panel) is { } waiting)
            Bind(waiting, panel, pending.Remake);
        if (panel.GetValue(AreaHostProperty) is ReactorHostControl host && Hosts.TryGetValue(host, out var mounted))
            return mounted;
        return null;
    }

    private static MountedHost Of(ReactorHostControl host)
    {
        if (Hosts.TryGetValue(host, out var mounted))
            return mounted;
        mounted = new MountedHost();
        Hosts.Add(host, mounted);
        return mounted;
    }

    private static ReactorHostControl? HostOf(DependencyObject start)
    {
        var node = start;
        while (true)
        {
            if (node is ReactorHostControl host)
                return host;
            var parent = node is FrameworkElement { Parent: { } logical } ? logical : VisualTreeHelper.GetParent(node);
            if (parent is null || ReferenceEquals(parent, node))
                return null;
            node = parent;
        }
    }

    /// <summary>
    /// The parent's child slot, read from the properties it has, so a new parent type needs no branch. The index is
    /// read again when the slot runs, in case the children moved.
    /// </summary>
    private static Action<UIElement>? Slot(UIElement control)
    {
        if (control is not FrameworkElement { Parent: { } parent })
            return null;

        var kind = parent.GetType();
        if (kind.GetProperty("Children")?.GetValue(parent) is UIElementCollection children &&
            children.IndexOf(control) >= 0)
        {
            return fresh =>
            {
                var index = children.IndexOf(control);
                if (index >= 0)
                    children[index] = fresh;
            };
        }

        var child = kind.GetProperty("Child");
        if (child is { CanWrite: true } && ReferenceEquals(child.GetValue(parent), control))
            return fresh => child.SetValue(parent, fresh);

        var content = kind.GetProperty("Content");
        if (content is { CanWrite: true } && ReferenceEquals(content.GetValue(parent), control))
            return fresh => content.SetValue(parent, fresh);

        return null;
    }

    private sealed class Pending(Action remake)
    {
        public Action Remake { get; } = remake;
    }
}
