using System.Reflection;
using System.Reflection.Metadata;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Winshell;

[assembly: MetadataUpdateHandler(typeof(PanelReload))]

namespace Winshell;

internal static class PanelReload
{
    internal static void UpdateApplication(Type[]? updated)
    {
        if (updated is null || Shell.Running is not { } shell)
            return;
        shell.Loop.Post(() => Apply(shell, updated));
    }

    internal static void Apply(Shell shell, IReadOnlyCollection<Type> updated)
    {
        var edited = updated.Select(Written).ToHashSet();
        var own = edited.Select(type => type.Assembly).ToHashSet();
        foreach (var window in shell.Platform.Windows.Select(host => host.Client).ToList())
        {
            if (window.Content is not { } content)
                continue;
            List<UIElement> controls = [];
            List<Action> remakes = [];
            Find(content, edited, own, controls, remakes);
            foreach (var control in controls)
                MakeAgain(shell, window, control);
            foreach (var remake in remakes)
                remake();
        }
    }

    private static Type Written(Type type)
    {
        while (type.Name.StartsWith('<') && type.DeclaringType is { } outer)
            type = outer;
        return type;
    }

    private static void Find(DependencyObject element, HashSet<Type> edited, HashSet<Assembly> own,
        List<UIElement> controls, List<Action> remakes)
    {
        if (AreaSessions.TryGet(element, out _))
        {
            Shows(element, edited, remakes);
            return;
        }

        if (element is UIElement control && own.Contains(element.GetType().Assembly))
        {
            List<Action> inside = [];
            if (Shows(control, edited, inside))
                controls.Add(control);
            else
                remakes.AddRange(inside);
            return;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            Find(VisualTreeHelper.GetChild(element, i), edited, own, controls, remakes);
    }

    private static bool Shows(DependencyObject element, HashSet<Type> edited, List<Action> remakes)
    {
        if (edited.Contains(element.GetType()))
            return true;
        List<Action> inside = AreaSessions.TryGet(element, out _) ? [] : remakes;
        var shows = false;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            shows |= Shows(VisualTreeHelper.GetChild(element, i), edited, inside);
        if (!AreaSessions.TryGet(element, out var remake))
            return shows;
        if (shows || inside.Count > 0)
            remakes.Add(remake);
        return false;
    }

    private static void MakeAgain(Shell shell, ShellWindow window, UIElement control)
    {
        var type = control.GetType();
        if (type.GetConstructor(Type.EmptyTypes) is not { } constructor)
        {
            shell.Log.Info("dev", $"{type.Name} needs arguments, so it stays. Edited methods still run.");
            return;
        }

        Action<UIElement>? place = ReferenceEquals(window.Content, control)
            ? fresh => window.Content = fresh
            : (control as FrameworkElement)?.Parent switch
            {
                Panel panel when panel.Children.IndexOf(control) is var at and >= 0 =>
                    fresh => panel.Children[at] = fresh,
                ContentControl holder when ReferenceEquals(holder.Content, control) => fresh => holder.Content = fresh,
                Border border when ReferenceEquals(border.Child, control) => fresh => border.Child = fresh,
                _ => null
            };
        if (place is null)
        {
            shell.Log.Info("dev", $"{type.Name} has no place a new one can go, so it stays. Edited methods still run.");
            return;
        }

        try
        {
            var fresh = (UIElement)constructor.Invoke(null);
            place(fresh);
        }
        catch (TargetInvocationException e)
        {
            shell.Log.Warn("dev", $"Could not rebuild {type.Name}: {e.InnerException?.Message}");
            return;
        }

        (control as IDisposable)?.Dispose();
        shell.Log.Info("dev", $"Rebuilt {type.Name}.");
    }
}
