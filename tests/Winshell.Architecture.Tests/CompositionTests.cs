using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Winshell.Generators;

namespace Winshell.Architecture.Tests;

public class CompositionTests
{
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
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("Composition", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        CSharpGeneratorDriver.Create(new PreferenceGenerator()).WithUpdatedParseOptions(options)
            .RunGeneratorsAndUpdateCompilation(compilation, out var generated, out var generatorErrors, cancel);
        var errors = generatorErrors.Concat(generated.GetDiagnostics(cancel))
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .ToList();

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }
}
