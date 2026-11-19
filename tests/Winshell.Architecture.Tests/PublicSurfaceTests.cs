using System.Reflection;

namespace Winshell.Architecture.Tests;

public class PublicSurfaceTests
{
    private const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    private static readonly Assembly[] Library =
    [
        Assembly.Load("Winshell.Native"), Assembly.Load("Winshell.Core"), Assembly.Load("Winshell.Services"),
        Assembly.Load("Winshell.Shell")
    ];

    [Fact]
    public void No_public_type_is_in_Winshell_Diagnostics()
    {
        var exposed = Library.SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.Namespace == "Winshell.Diagnostics")
            .Select(type => type.FullName)
            .ToList();

        Assert.True(exposed.Count == 0,
            $"Make these internal: winshell doctor owns its checks. {string.Join(", ", exposed)}");
    }

    [Theory]
    [InlineData("Winshell.IEventLoop")]
    [InlineData("Winshell.IShellPart")]
    [InlineData("Winshell.Surface")]
    [InlineData("Winshell.SurfaceProps")]
    [InlineData("Winshell.WidgetArea")]
    [InlineData("Winshell.WidgetAreaProps")]
    public void An_internal_type_stays_internal(string name)
    {
        var type = Library.Select(assembly => assembly.GetType(name)).OfType<Type>().Single();

        Assert.False(type.IsVisible, $"{name} is public. Make it internal; an author reaches it through Elements or Shell.");
    }

    [Theory]
    [InlineData("Winshell.Shell", "Dispose")]
    [InlineData("Winshell.Shell", "Loop")]
    [InlineData("Winshell.Shell", "Options")]
    [InlineData("Winshell.Shell", "Remove")]
    [InlineData("Winshell.Log", "Default")]
    [InlineData("Winshell.Placement", "Check")]
    [InlineData("Winshell.Placement", "Parse")]
    [InlineData("Winshell.Placement", "Format")]
    public void An_internal_member_stays_internal(string typeName, string member)
    {
        var type = Library.Select(assembly => assembly.GetType(typeName)).OfType<Type>().Single();

        Assert.True(type.IsVisible, $"{typeName} is not public.");
        Assert.True(type.GetMember(member, Public).Length == 0, $"{typeName}.{member} is public. Make it internal.");
    }

    [Fact]
    public void Only_Windows_makes_a_ScreenInfo()
    {
        var screen = Assembly.Load("Winshell.Services").GetType("Winshell.ScreenInfo", true)!;

        Assert.Empty(screen.GetConstructors(Public));
        Assert.DoesNotContain(screen.GetProperties(Public), property => property.SetMethod is { IsPublic: true });
    }

    [Fact]
    public void A_service_the_shell_shares_cannot_be_disposed()
    {
        var shell = Assembly.Load("Winshell.Shell").GetType("Winshell.Shell", true)!;
        var services = shell.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.PropertyType)
            .Where(type => Library.Contains(type.Assembly))
            .ToList();
        Assert.Contains(services, type => type.FullName == "Winshell.Audio.AudioService");

        var disposable = services.Where(typeof(IDisposable).IsAssignableFrom).Select(type => type.Name).ToList();

        Assert.True(disposable.Count == 0,
            $"The shell owns its services. Implement the internal IShellService instead of IDisposable on {string.Join(", ", disposable)}.");
    }

    [Fact]
    public void A_shell_cannot_be_disposed()
    {
        var shell = Assembly.Load("Winshell.Shell").GetType("Winshell.Shell", true)!;

        Assert.False(typeof(IDisposable).IsAssignableFrom(shell), "Shell is IDisposable. shell.Quit() stops the shell.");
    }
}
