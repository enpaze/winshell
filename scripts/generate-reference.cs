#!/usr/bin/env dotnet
// Writes the reference pages, docs/reference/winshell*.md: a page per namespace of the Winshell package, from the
// public types of Winshell.Shell, Winshell.Services and Winshell.Core and their /// comments. A page keeps what is
// above its marker line, and this writes everything below it. Run it after changing a public member, and commit the
// pages it writes. The page writer is reference-pages.cs, which has no entry point of its own; the shebang
// marks this file as the one dotnet run starts.
//
//   dotnet run scripts/generate-reference.cs

#:property PublishAot=false
#:property RuntimeIdentifier=$(NETCoreSdkRuntimeIdentifier)
#:project ../src/Winshell.Shell/Winshell.Shell.csproj
#:include reference-pages.cs

namespace Winshell.Docs;

internal static class Program
{
    private static void Main()
    {
        var scripts = AppContext.GetData("EntryPointFileDirectoryPath") as string ?? ".";
        var folder = Path.GetFullPath(Path.Combine(scripts, "..", "docs", "reference"));
        var pages = ReferencePages.Generate(folder);
        foreach (var file in ReferencePages.Stale(folder, pages))
        {
            if (pages.TryGetValue(file, out var text))
            {
                File.WriteAllText(Path.Combine(folder, file), text);
                Console.WriteLine($"wrote docs/reference/{file}");
            }
            else
            {
                File.Delete(Path.Combine(folder, file));
                Console.WriteLine($"deleted docs/reference/{file}, whose namespace is gone");
            }
        }
    }
}
