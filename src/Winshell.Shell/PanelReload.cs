using System.Reflection;
using System.Reflection.Metadata;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Winshell;

[assembly: MetadataUpdateHandler(typeof(PanelReload))]

namespace Winshell;

/// <summary>
/// Called by .NET when <c>dotnet watch</c> applies an edit to the running program. An edited method already runs its new
/// code. What a constructor built does not, so a widget area showing an edited type makes its widgets again, and a window
/// showing one anywhere else gets a new one of its content. Every other window, and the window itself with its place and
/// state, stays as it is. A bar's surface, say, is filled by the code that made it, which its constructor can't repeat.
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
        foreach (var window in shell.Platform.Windows.Select(host => host.Client).ToList())
        {
            if (window.Content is not { } content)
                continue;
            List<WidgetArea> areas = [];
            if (Shows(content, edited, areas))
                Rebuild(shell, window, content);
            else
                foreach (var area in areas)
                    area.Rebuild();
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
    /// Whether <paramref name="element"/> shows an edited type outside a widget area. Each outermost area that shows one
    /// goes in <paramref name="areas"/> instead, since an area can make its widgets again.
    /// </summary>
    private static bool Shows(DependencyObject element, HashSet<Type> edited, List<WidgetArea> areas)
    {
        if (edited.Contains(element.GetType()))
            return true;
        List<WidgetArea> inside = element is WidgetArea ? [] : areas;
        var shows = false;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            shows |= Shows(VisualTreeHelper.GetChild(element, i), edited, inside);
        if (element is not WidgetArea area)
            return shows;
        if (shows || inside.Count > 0)
            areas.Add(area);
        return false;
    }

    private static void Rebuild(Shell shell, ShellWindow window, UIElement content)
    {
        var type = content.GetType();
        if (type.GetConstructor(Type.EmptyTypes) is not { } constructor)
        {
            shell.Log.Info("dev", $"{type.Name} needs arguments, so its window keeps it. Edited methods still run.");
            return;
        }

        try
        {
            var fresh = (UIElement)constructor.Invoke(null);
            XamlReload.Refresh(fresh);
            window.Content = fresh;
        }
        catch (TargetInvocationException e)
        {
            // Half-typed code is normal here. The last version keeps running until the next edit.
            shell.Log.Warn("dev", $"Could not rebuild {type.Name}: {e.InnerException?.Message}");
            return;
        }

        (content as IDisposable)?.Dispose();
        shell.Log.Info("dev", $"Rebuilt {type.Name}.");
    }
}
