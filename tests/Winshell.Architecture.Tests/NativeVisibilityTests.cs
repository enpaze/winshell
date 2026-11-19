using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Winshell.Architecture.Tests;

[TestClass]
public class NativeVisibilityTests
{
    [TestMethod]
    public void Win32_cs_declares_no_native_method()
    {
        var path = Path.Combine(Repo.Root, "src", "Winshell.Native", "Win32.cs");
        var cancel = CurrentTest.Cancellation;
        var imports = CSharpSyntaxTree.ParseText(File.ReadAllText(path), null, "", null, cancel).GetRoot(cancel)
            .DescendantNodes()
            .OfType<AttributeSyntax>()
            .Select(attribute => Bare(attribute.Name.ToString()))
            .Where(name => name is "DllImport" or "LibraryImport" or "ComImport" or "GeneratedComInterface"
                or "GeneratedComClass")
            .ToList();

        Assert.IsEmpty(imports);
    }

    private static string Bare(string name)
    {
        var last = name[(name.LastIndexOf('.') + 1)..];
        return last.EndsWith("Attribute", StringComparison.Ordinal) ? last[..^"Attribute".Length] : last;
    }
}
