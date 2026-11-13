using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Winshell.Architecture.Tests;

/// <summary>
/// Last-input time is measured on <c>GetTickCount</c>. Only <c>SentInput</c> reads that clock in <c>src/</c>.
/// </summary>
public class InputClockTests
{
    [Fact]
    public void Only_SentInput_reads_the_tick_count()
    {
        var src = Path.Combine(Repo.Root, "src");
        var readers = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Split(Path.DirectorySeparatorChar).All(part => part is not ("bin" or "obj")))
            .Where(ReadsTickCount)
            .Select(path => Path.GetRelativePath(src, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["Winshell.Native/Input/SentInput.cs"], readers);
    }

    // A call to GetTickCount, or Environment.TickCount. The declaration of GetTickCount is not a read.
    private static bool ReadsTickCount(string path)
    {
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path));
        return tree.GetRoot().DescendantNodes().Any(node => node switch
        {
            InvocationExpressionSyntax call => Name(call.Expression) is "GetTickCount" or "GetTickCount64",
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText is "TickCount" or "TickCount64",
            _ => false
        });
    }

    private static string? Name(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            _ => null
        };
    }
}
