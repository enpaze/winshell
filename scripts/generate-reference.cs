#!/usr/bin/env dotnet

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
