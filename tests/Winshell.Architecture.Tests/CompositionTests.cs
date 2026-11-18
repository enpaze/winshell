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
    public void A_composition_compiles_against_the_public_API_and_the_shells_own_internals()
    {
        var errors = Compile();

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    [Theory]
    [InlineData("Winshell.AtomicFile")]
    [InlineData("Winshell.Native.HotkeyTarget")]
    [InlineData("Winshell.Input.HotkeyRegistrar")]
    public void A_composition_cannot_use_core_native_or_services_internals(string type)
    {
        var errors = Compile($"static class UsesInternal {{ static readonly Type Used = typeof({type}); }}");

        Assert.Contains(errors, error => error.Contains("CS0122", StringComparison.Ordinal) &&
                                         error.Contains(type[(type.LastIndexOf('.') + 1)..], StringComparison.Ordinal));
    }

    private static List<string> Compile(string? extra = null)
    {
        var files = Directory.GetFiles(Path.Combine(Repo.Root, "src", "Winshell.Shell", "Compositions"), "*.cs",
            SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var cancel = TestContext.Current.CancellationToken;
        var trees = files.Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), options, file, null, cancel))
            .Append(CSharpSyntaxTree.ParseText(ImplicitUsings, options, "", null, cancel));
        if (extra is not null)
            trees = trees.Append(CSharpSyntaxTree.ParseText(extra, options, "extra.cs", null, cancel));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("Winshell.Compositions", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        CSharpGeneratorDriver.Create(new PreferenceGenerator()).WithUpdatedParseOptions(options)
            .RunGeneratorsAndUpdateCompilation(compilation, out var generated, out var generatorErrors, cancel);
        return generatorErrors.Concat(generated.GetDiagnostics(cancel))
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .ToList();
    }
}
