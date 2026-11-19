using System.Reflection;

namespace Winshell.Architecture.Tests;

[TestClass]
public class PublicSurfaceTests
{
    private const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    private static readonly Assembly[] Library =
    [
        Assembly.Load("Winshell.Native"), Assembly.Load("Winshell.Core"), Assembly.Load("Winshell.Services"),
        Assembly.Load("Winshell.Shell")
    ];

    [TestMethod]
    public void No_public_type_is_in_Winshell_Diagnostics()
    {
        var exposed = Library.SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.Namespace == "Winshell.Diagnostics")
            .Select(type => type.FullName)
            .ToList();

        Assert.IsTrue(exposed.Count == 0,
            $"Make these internal: winshell doctor owns its checks. {string.Join(", ", exposed)}");
    }

    [TestMethod]
    [DataRow("Winshell.IEventLoop")]
    [DataRow("Winshell.IShellPart")]
    [DataRow("Winshell.Surface")]
    [DataRow("Winshell.SurfaceProps")]
    [DataRow("Winshell.WidgetArea")]
    [DataRow("Winshell.WidgetAreaProps")]
    public void An_internal_type_stays_internal(string name)
    {
        var type = Library.Select(assembly => assembly.GetType(name)).OfType<Type>().Single();

        Assert.IsFalse(type.IsVisible, $"{name} is public. Make it internal; an author reaches it through Elements or Shell.");
    }

    [TestMethod]
    [DataRow("Winshell.Shell", "Dispose")]
    [DataRow("Winshell.Shell", "Loop")]
    [DataRow("Winshell.Shell", "Options")]
    [DataRow("Winshell.Shell", "Remove")]
    [DataRow("Winshell.Log", "Default")]
    [DataRow("Winshell.Placement", "Check")]
    [DataRow("Winshell.Placement", "Parse")]
    [DataRow("Winshell.Placement", "Format")]
    public void An_internal_member_stays_internal(string typeName, string member)
    {
        var type = Library.Select(assembly => assembly.GetType(typeName)).OfType<Type>().Single();

        Assert.IsTrue(type.IsVisible, $"{typeName} is not public.");
        Assert.IsTrue(type.GetMember(member, Public).Length == 0, $"{typeName}.{member} is public. Make it internal.");
    }

    [TestMethod]
    public void Only_Windows_makes_a_ScreenInfo()
    {
        var screen = Assembly.Load("Winshell.Services").GetType("Winshell.ScreenInfo", true)!;

        Assert.IsEmpty(screen.GetConstructors(Public));
        Assert.DoesNotContain(property => property.SetMethod is { IsPublic: true }, screen.GetProperties(Public));
    }

    [TestMethod]
    public void A_service_the_shell_shares_cannot_be_disposed()
    {
        var shell = Assembly.Load("Winshell.Shell").GetType("Winshell.Shell", true)!;
        var services = shell.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.PropertyType)
            .Where(type => Library.Contains(type.Assembly))
            .ToList();
        Assert.Contains(type => type.FullName == "Winshell.Audio.AudioService", services);

        var disposable = services.Where(typeof(IDisposable).IsAssignableFrom).Select(type => type.Name).ToList();

        Assert.IsTrue(disposable.Count == 0,
            $"The shell owns its services. Implement the internal IShellService instead of IDisposable on {string.Join(", ", disposable)}.");
    }

    [TestMethod]
    public void A_shell_cannot_be_disposed()
    {
        var shell = Assembly.Load("Winshell.Shell").GetType("Winshell.Shell", true)!;

        Assert.IsFalse(typeof(IDisposable).IsAssignableFrom(shell), "Shell is IDisposable. shell.Quit() stops the shell.");
    }
}
