using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Winshell.Architecture.Tests;

/// <summary>
/// CsWin32 is the Win32 declaration set. <c>Win32.cs</c> keeps <c>POINT</c> and the words of a window message, which
/// CsWin32 does not generate, and declares no native method of its own.
/// </summary>
public class NativeVisibilityTests
{
    [Fact]
    public void Win32_cs_declares_no_native_method()
    {
        var path = Path.Combine(Repo.Root, "src", "Winshell.Native", "Win32.cs");
        var cancel = TestContext.Current.CancellationToken;
        var imports = CSharpSyntaxTree.ParseText(File.ReadAllText(path), null, "", null, cancel).GetRoot(cancel)
            .DescendantNodes()
            .OfType<AttributeSyntax>()
            .Select(attribute => Bare(attribute.Name.ToString()))
            .Where(name => name is "DllImport" or "LibraryImport" or "ComImport" or "GeneratedComInterface"
                or "GeneratedComClass")
            .ToList();

        Assert.Empty(imports);
    }

    // An attribute can be written qualified, or with its Attribute suffix.
    private static string Bare(string name)
    {
        var last = name[(name.LastIndexOf('.') + 1)..];
        return last.EndsWith("Attribute", StringComparison.Ordinal) ? last[..^"Attribute".Length] : last;
    }
}
