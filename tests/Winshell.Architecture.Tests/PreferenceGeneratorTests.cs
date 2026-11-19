using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Winshell.Generators;

namespace Winshell.Architecture.Tests;

public class PreferenceGeneratorTests
{
    [Fact]
    public void A_preference_in_a_nested_generic_class_with_a_private_setter_compiles()
    {
        var (generator, compiler) = Compile("""
            namespace Mine;

            public partial class Outer<T>
            {
                public sealed partial class Choices() : Winshell.Preferences("choices")
                {
                    [Winshell.Preference]
                    public partial System.Collections.Generic.IReadOnlyList<string?>? Pins { get; private set; } = ["a"];
                }
            }
            """);

        Assert.Empty(generator);
        Assert.Empty(compiler);
    }

    [Fact]
    public void A_preference_in_the_global_namespace_with_nullable_off_compiles()
    {
        var (generator, compiler) = Compile("""
            #nullable disable
            public sealed partial class Choices() : Winshell.Preferences("choices")
            {
                [Winshell.Preference]
                public partial string Name { get; set; } = "a";
            }
            """);

        Assert.Empty(generator);
        Assert.Empty(compiler);
    }

    [Fact]
    public void Preferences_of_classes_whose_names_differ_only_in_punctuation_arity_or_case_compile_in_one_project()
    {
        var (generator, compiler) = Compile("""
            namespace Mine;

            public sealed partial class C<T>() : Winshell.Preferences("generic")
            {
                [Winshell.Preference]
                public partial string Name { get; set; } = "a";
            }

            public sealed partial class C_T_() : Winshell.Preferences("underscores")
            {
                [Winshell.Preference]
                public partial string Name { get; set; } = "b";
            }

            public sealed partial class C() : Winshell.Preferences("upper")
            {
                [Winshell.Preference]
                public partial string Name { get; set; } = "c";
            }

            public sealed partial class c() : Winshell.Preferences("lower")
            {
                [Winshell.Preference]
                public partial string Name { get; set; } = "d";
            }
            """);

        Assert.Empty(generator);
        Assert.Empty(compiler);
    }

    [Fact]
    public void A_preference_whose_names_are_keywords_compiles()
    {
        var (generator, compiler) = Compile("""
            namespace @namespace.@event;

            public partial class @class<@int>
            {
                public sealed partial class @struct() : Winshell.Preferences("keywords")
                {
                    [Winshell.Preference]
                    public partial bool @event { get; set; }
                }
            }
            """);

        Assert.Empty(generator);
        Assert.Empty(compiler);
    }

    [Fact]
    public void A_preference_whose_names_are_contextual_keywords_compiles()
    {
        var (generator, compiler) = Compile("""
            namespace Mine;

            public partial class @record
            {
                public sealed partial class @var() : Winshell.Preferences("contextual")
                {
                    [Winshell.Preference]
                    public partial bool @value { get; set; }
                }
            }
            """);

        Assert.Empty(generator);
        Assert.Empty(compiler);
    }

    [Theory]
    [InlineData("public bool Clock24 { get; set; }")]
    [InlineData("public static partial bool Clock24 { get; set; }")]
    public void A_preference_that_is_not_an_instance_partial_property_is_an_error_that_shows_the_partial_form(
        string declaration)
    {
        var source = $$"""
            public sealed partial class Choices : Winshell.Preferences
            {
                [Winshell.Preference]
                {{declaration}}
            }
            """;
        var (generator, _) = Compile(source);

        var error = Assert.Single(generator);
        Assert.Equal(("WINSHELL001", DiagnosticSeverity.Error), (error.Id, error.Severity));
        Assert.Equal("Clock24", Named(source, error));
        Assert.Contains("'public partial bool Clock24 { get; set; }'", error.GetMessage(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_preference_with_accessors_of_its_own_is_an_error()
    {
        var (generator, _) = Compile("""
            public sealed partial class Choices : Winshell.Preferences
            {
                public partial bool Clock24 { get; set; }

                [Winshell.Preference]
                public partial bool Clock24 { get => field; set => field = value; }
            }
            """);

        Assert.Equal("WINSHELL001", Assert.Single(generator).Id);
    }

    [Fact]
    public void A_preference_outside_a_Preferences_class_is_an_error_that_names_the_class()
    {
        var source = """
            public sealed partial class Choices : Winshell.ObservableObject
            {
                [Winshell.Preference]
                public partial bool Clock24 { get; set; }
            }
            """;
        var (generator, _) = Compile(source);

        var error = Assert.Single(generator);
        Assert.Equal(("WINSHELL002", DiagnosticSeverity.Error), (error.Id, error.Severity));
        Assert.Equal("Clock24", Named(source, error));
        Assert.Equal("Clock24 is a preference, so Choices must derive from Winshell.Preferences",
            error.GetMessage(CultureInfo.InvariantCulture));
    }

    private static string Named(string source, Diagnostic diagnostic)
    {
        Assert.Equal("Choices.cs", diagnostic.Location.GetLineSpan().Path);
        return source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length);
    }

    private static (List<Diagnostic> Generator, List<Diagnostic> Compiler) Compile(string source)
    {
        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var cancel = TestContext.Current.CancellationToken;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("Author",
            [CSharpSyntaxTree.ParseText(source, options, "Choices.cs", null, cancel)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        CSharpGeneratorDriver.Create(new PreferenceGenerator()).WithUpdatedParseOptions(options)
            .RunGeneratorsAndUpdateCompilation(compilation, out var generated, out var diagnostics, cancel);
        return ([.. diagnostics], [.. generated.GetDiagnostics(cancel).Where(d => d.Severity >= DiagnosticSeverity.Warning)]);
    }
}
