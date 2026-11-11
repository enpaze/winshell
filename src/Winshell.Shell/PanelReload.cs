using System.Reflection;
using System.Reflection.Metadata;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Winshell;

[assembly: MetadataUpdateHandler(typeof(PanelReload))]

namespace Winshell;

/// <summary>
/// Called by .NET when <c>dotnet watch</c> applies an edit to the running program. An edited method already runs its new
/// code. What a constructor built does not, so a widget area showing an edited type makes its widgets again, and
/// anywhere else the outermost of the configuration's own controls around one, a type from an edited assembly, is made
/// again in its place. The mounted host says which areas those are, and where a control it shows goes back. A
/// control's constructor builds what it shows. A Winshell or WinUI control around it, such as a grid filled in code,
/// is filled by whoever made it, which its constructor can't repeat, so it stays with the rest of what it holds.
/// </summary>
internal static class PanelReload
{
    /// <summary>Called by name by .NET, on no thread of ours.</summary>
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
            try
            {
                Find(content, edited, own, controls, remakes);
                foreach (var control in controls)
                    MakeAgain(shell, window, control);
                foreach (var remake in remakes)
                    remake();
            }
            finally
            {
                // A place the host recorded keeps the old control alive. Drop it whether or not the rebuild used it.
                foreach (var control in controls)
                    MountedHost.Forget(control);
            }
        }
    }

    /// <summary>An edit reports the closures and state machines the compiler made, which are nested in the type it changed.</summary>
    private static Type Written(Type type)
    {
        while (type.Name.StartsWith('<') && type.DeclaringType is { } outer)
            type = outer;
        return type;
    }

    /// <summary>
    /// Finds what to make again in <paramref name="element"/>. A widget area that shows an edited type goes in
    /// <paramref name="remakes"/>. So does one inside the outermost control of a type from an edited assembly, the
    /// configuration's own, unless that control shows an edited type outside every area: then it goes in
    /// <paramref name="controls"/>.
    /// </summary>
    private static void Find(DependencyObject element, HashSet<Type> edited, HashSet<Assembly> own,
        List<UIElement> controls, List<Action> remakes)
    {
        if (MountedHost.TryRemake(element, out _))
        {
            Shows(element, edited, remakes);
            return;
        }

        if (element is UIElement control && own.Contains(element.GetType().Assembly))
        {
            List<Action> inside = [];
            if (Shows(control, edited, inside))
            {
                MountedHost.Remember(control);
                controls.Add(control);
            }
            else
            {
                remakes.AddRange(inside);
            }

            return;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            Find(VisualTreeHelper.GetChild(element, i), edited, own, controls, remakes);
    }

    /// <summary>
    /// Whether <paramref name="element"/> shows an edited type outside a widget area. Each outermost area that shows one
    /// goes in <paramref name="remakes"/> instead, since an area can make its widgets again.
    /// </summary>
    private static bool Shows(DependencyObject element, HashSet<Type> edited, List<Action> remakes)
    {
        if (edited.Contains(element.GetType()))
            return true;
        var area = MountedHost.TryRemake(element, out var remake);
        List<Action> inside = area ? [] : remakes;
        var shows = false;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            shows |= Shows(VisualTreeHelper.GetChild(element, i), edited, inside);
        if (!area)
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

        // The window's content is its own slot. Replacing the grid child under it would leave the window pointing at
        // the control this just removed.
        Action<UIElement>? place = ReferenceEquals(window.Content, control)
            ? fresh => window.Content = fresh
            : MountedHost.TakePlace(control);
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
            // Half-typed code is normal here. The last version keeps running until the next edit.
            shell.Log.Warn("dev", $"Could not rebuild {type.Name}: {e.InnerException?.Message}");
            return;
        }

        (control as IDisposable)?.Dispose();
        shell.Log.Info("dev", $"Rebuilt {type.Name}.");
    }
}
