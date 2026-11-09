using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Winshell.Power;

namespace Winshell.Services.Tests;

public sealed class ServiceReadTests
{
    // Methods that do something and answer whether it worked, or with what they made. A watch doesn't call them, and
    // the guard doesn't run them.
    private static readonly HashSet<string> Actions =
    [
        "CaptureSession.CopyToClipboard", "CaptureSession.SaveToFile", "CaptureSession.Start",
        "KeyboardLayoutService.Next", "KeyboardLayoutService.Use", "Notification.Dismiss", "Notification.Invoke",
        "NotificationGroup.Dismiss", "NotificationListener.ClearAll", "ToastMessage.Send",
        "VirtualDesktopsService.Create", "WallpaperService.SetBackground", "WallpaperService.SetFit",
        "WallpaperService.SetImage"
    ];

    /// <summary>
    /// A watch follows only what reported its read, so a property that does not report leaves a control stale. Each
    /// service type is made without running a constructor, so a new one is checked without a fake of its own.
    /// </summary>
    [Fact]
    public void Every_public_property_of_a_service_type_reports_its_read()
    {
        var untracked = new List<string>();
        foreach (var type in ServiceTypes())
        {
            var service = (INotifyPropertyChanged)RuntimeHelpers.GetUninitializedObject(type);
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(property => property.GetMethod is { IsPublic: true } &&
                                            property.GetIndexParameters().Length == 0))
            {
                var reads = new Reads();
                try
                {
                    reads.Record(() => property.GetValue(service));
                }
                catch (TargetInvocationException)
                {
                    // A getter reports before it computes, so one that needs the state a constructor makes has
                    // reported by the time it throws.
                }

                if (!reads.Properties.TryGetValue(service, out var names) || !names.Contains(property.Name))
                    untracked.Add($"{type.Name}.{property.Name}");
            }
        }

        Assert.True(untracked.Count == 0, $"These properties do not report their read: {string.Join(", ", untracked)}.");
    }

    /// <summary>
    /// A method that answers, as <c>MonitorFor</c> does, is read like a property, so it reports what it answers from
    /// before it computes anything. Each is called on a service made without a constructor, with each argument its
    /// type's default. It shows that a method reports a read, not that it reports all it read: each service's tests show
    /// that. Methods <see cref="object"/> declares, such as <c>ToString</c>, and the <see cref="Actions"/> are left
    /// out.
    /// </summary>
    [Fact]
    public void Every_public_method_of_a_service_type_that_answers_reports_a_read()
    {
        var untracked = new List<string>();
        foreach (var type in ServiceTypes())
        {
            var service = RuntimeHelpers.GetUninitializedObject(type);
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                         .Where(method => method is { IsSpecialName: false } && method.ReturnType != typeof(void) &&
                                          method.GetBaseDefinition().DeclaringType != typeof(object) &&
                                          !Actions.Contains($"{type.Name}.{method.Name}")))
            {
                var arguments = method.GetParameters()
                    .Select(parameter => parameter.ParameterType.IsValueType
                        ? Activator.CreateInstance(parameter.ParameterType)
                        : null)
                    .ToArray();
                var reads = new Reads();
                try
                {
                    reads.Record(() => method.Invoke(service, arguments));
                }
                catch (TargetInvocationException)
                {
                    // A method reports before it computes, as a getter does.
                }

                if (reads.Properties.Count == 0 && reads.Lists.Count == 0)
                    untracked.Add($"{type.Name}.{method.Name}");
            }
        }

        Assert.True(untracked.Count == 0, $"These methods do not report a read: {string.Join(", ", untracked)}.");
    }

    /// <summary>
    /// Whether <paramref name="change"/> changes something <paramref name="block"/> read, so that a watch of the block
    /// would run it again.
    /// </summary>
    internal static bool ChangesWhatItRead(Action block, Action change)
    {
        var reads = new Reads();
        reads.Record(block);
        var changed = false;
        PropertyChangedEventHandler onProperty = (sender, e) =>
            changed |= string.IsNullOrEmpty(e.PropertyName) ||
                       reads.Properties[(INotifyPropertyChanged)sender!].Contains(e.PropertyName);
        NotifyCollectionChangedEventHandler onList = (_, _) => changed = true;
        foreach (var source in reads.Properties.Keys)
            source.PropertyChanged += onProperty;
        foreach (var list in reads.Lists)
            list.CollectionChanged += onList;
        try
        {
            change();
        }
        finally
        {
            foreach (var source in reads.Properties.Keys)
                source.PropertyChanged -= onProperty;
            foreach (var list in reads.Lists)
                list.CollectionChanged -= onList;
        }

        return changed;
    }

    private static List<Type> ServiceTypes()
    {
        var services = typeof(PowerService).Assembly.GetExportedTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } &&
                           typeof(INotifyPropertyChanged).IsAssignableFrom(type))
            .ToList();
        Assert.Contains(typeof(PowerService), services);
        return services;
    }
}
