using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Winshell.Architecture.Tests;

/// <summary>
/// A composition, such as <c>Bar</c>, uses only the API a configuration has, as ADR 0001 decides. It sits in
/// <c>src/Winshell.Shell/Compositions</c>, where the compiler lets it see Shell's internals and those Core and Services
/// show Shell. So this compiles that folder again as an assembly of its own against the built Winshell assemblies,
/// which shows it only their public API.
/// </summary>
public class CompositionTests
{
    // The global usings the SDK gives a project with ImplicitUsings, as Winshell.Shell has.
    private const string ImplicitUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Net.Http;
        global using System.Threading;
        global using System.Threading.Tasks;
        """;

    [Fact]
    public void A_composition_compiles_against_only_the_public_API()
    {
        var files = Directory.GetFiles(Path.Combine(Repo.Root, "src", "Winshell.Shell", "Compositions"), "*.cs",
            SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var cancel = TestContext.Current.CancellationToken;
        var trees = files.Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), options, file, null, cancel))
            .Append(CSharpSyntaxTree.ParseText(ImplicitUsings, options, "", null, cancel));
        // The test's own dependencies: the framework, the Winshell assemblies and WinUI, as a configuration has them.
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));

        var errors = CSharpCompilation.Create("Composition", trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable))
            .GetDiagnostics(cancel)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .ToList();

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }
}
