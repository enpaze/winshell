using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Winshell.Power;

namespace Winshell.Services.Tests;

public sealed class ServiceReadTests
{
    /// <summary>
    /// A watch follows only what reported its read, so a property that does not report leaves a control stale. Each
    /// service type is made without running a constructor, so a new one is checked without a fake of its own.
    /// </summary>
    [Fact]
    public void Every_public_property_of_a_service_type_reports_its_read()
    {
        var services = typeof(PowerService).Assembly.GetExportedTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } &&
                           typeof(INotifyPropertyChanged).IsAssignableFrom(type))
            .ToList();
        Assert.Contains(typeof(PowerService), services);

        var untracked = new List<string>();
        foreach (var type in services)
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
}
