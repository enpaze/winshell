#!/usr/bin/env dotnet
#:property PublishAot=false
#:package Microsoft.CodeAnalysis.CSharp

using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Winshell.Comments;

internal static class Program
{
    private static readonly SyntaxKind[] CommentKinds =
    [
        SyntaxKind.SingleLineCommentTrivia,
        SyntaxKind.MultiLineCommentTrivia,
        SyntaxKind.SingleLineDocumentationCommentTrivia,
        SyntaxKind.MultiLineDocumentationCommentTrivia
    ];

    private static void Main()
    {
        while (Console.ReadLine() is { } path)
        {
            if (path.Length == 0)
                continue;
            var text = File.ReadAllText(path);
            var spans = path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? FenceSpans(text) : Spans(text, 0);
            foreach (var span in spans)
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{path}\t{span.Start}\t{span.Length}"));
        }
    }

    private static IEnumerable<TextSpan> Spans(string text, int offset)
    {
        return CSharpSyntaxTree.ParseText(text).GetRoot().DescendantTrivia(descendIntoTrivia: true)
            .Where(trivia => CommentKinds.Contains(trivia.Kind()))
            .Select(trivia => new TextSpan(offset + trivia.FullSpan.Start, trivia.FullSpan.Length));
    }

    private static IEnumerable<TextSpan> FenceSpans(string text)
    {
        var start = -1;
        var position = 0;
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (start < 0 && trimmed == "```csharp")
                start = position + line.Length + 1;
            else if (start >= 0 && trimmed == "```")
            {
                foreach (var span in Spans(text[start..position], start))
                    yield return span;
                start = -1;
            }

            position += line.Length + 1;
        }
    }
}
