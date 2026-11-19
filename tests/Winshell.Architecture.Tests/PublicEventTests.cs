using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Xml.Linq;

namespace Winshell.Architecture.Tests;

public class PublicEventTests
{
    [Fact]
    public void Every_public_event_is_an_Action()
    {
        var shipped = XDocument
            .Load(Path.Combine(Repo.Root, "src", "Winshell.Package", "Winshell.Package.csproj"))
            .Descendants("ProjectReference")
            .Where(reference => reference.Attribute("ReferenceOutputAssembly")?.Value != "false")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value))
            .ToList();

        var offenders = shipped
            .Select(Assembly.Load)
            .SelectMany(assembly => assembly.GetExportedTypes())
            .SelectMany(type => type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                                               BindingFlags.DeclaredOnly)
                .Where(e => !IsAction(e.EventHandlerType!) && !Implements(type, e))
                .Select(e => $"{type.FullName}.{e.Name} is {e.EventHandlerType!.Name}; make it Action or Action<T>"))
            .ToList();

        Assert.Equal(["Winshell.Core", "Winshell.Native", "Winshell.Services", "Winshell.Shell"], shipped.Order());
        Assert.Empty(offenders);
    }

    private static bool IsAction(Type type)
    {
        return type == typeof(Action) || type is { IsGenericType: true, Namespace: "System" } &&
            type.GetGenericTypeDefinition().Name.StartsWith("Action`", StringComparison.Ordinal);
    }

    private static bool Implements(Type type, EventInfo e)
    {
        return new[] { typeof(INotifyPropertyChanged), typeof(INotifyCollectionChanged) }
            .Where(contract => contract.IsAssignableFrom(type))
            .Any(contract => type.GetInterfaceMap(contract).TargetMethods.Contains(e.AddMethod));
    }
}
