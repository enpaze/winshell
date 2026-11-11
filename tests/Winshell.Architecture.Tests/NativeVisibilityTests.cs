using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Winshell.Architecture.Tests;

/// <summary>
/// CsWin32 is the Win32 declaration set. <c>Win32.cs</c> keeps <c>POINT</c> and the words of a window message, which
/// CsWin32 does not generate. Native's internals are visible to the projects that call them.
/// </summary>
public class NativeVisibilityTests
{
    private static readonly string[] Friends =
    [
        "Winshell.Core",
        "Winshell.Services",
        "Winshell.Services.Tests",
        "Winshell.Shell",
        "Winshell.Shell.Tests",
        "Winshell.TestSupport",
        "Winshell.Tests"
    ];

    [Fact]
    public void Native_internals_are_visible_only_to_the_projects_that_call_them()
    {
        var names = XDocument.Load(Path.Combine(Repo.Root, "src", "Winshell.Native", "Winshell.Native.csproj"))
            .Descendants("InternalsVisibleTo")
            .Select(element => element.Attribute("Include")!.Value)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(Friends, names);
    }

    [Fact]
    public void Win32_cs_declares_no_native_method()
    {
        var path = Path.Combine(Repo.Root, "src", "Winshell.Native", "Win32.cs");
        var cancel = TestContext.Current.CancellationToken;
        var imports = CSharpSyntaxTree.ParseText(File.ReadAllText(path), null, "", null, cancel).GetRoot(cancel)
            .DescendantNodes()
            .OfType<AttributeSyntax>()
            .Select(attribute => attribute.Name.ToString())
            .Where(name => name is "DllImport" or "LibraryImport" or "ComImport")
            .ToList();

        Assert.Empty(imports);
    }
}
