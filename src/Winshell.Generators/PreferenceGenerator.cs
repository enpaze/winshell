using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Winshell.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class PreferenceGenerator : IIncrementalGenerator
{
    public static readonly DiagnosticDescriptor NotPartial = new(
        "WINSHELL001",
        "A preference is a partial property",
        "Declare {0} as 'public partial {1} {0} {{ get; set; }}': [Preference] writes the accessors of an instance partial property that has none of its own",
        "Winshell",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor OutsidePreferences = new(
        "WINSHELL002",
        "A preference belongs to a Preferences class",
        "{0} is a preference, so {1} must derive from Winshell.Preferences",
        "Winshell",
        DiagnosticSeverity.Error,
        true);

    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var preferences = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Winshell.PreferenceAttribute",
            static (node, _) => node is PropertyDeclarationSyntax,
            static (target, _) => Describe(target));
        context.RegisterSourceOutput(preferences, static (output, preference) =>
        {
            switch (preference)
            {
                case Problem problem:
                    output.ReportDiagnostic(Diagnostic.Create(problem.Rule,
                        Location.Create(problem.Path, problem.Span, problem.Lines), problem.Property, problem.Detail));
                    break;
                case Source source:
                    output.AddSource(source.HintName, source.Text);
                    break;
            }
        });
    }

    private static Outcome Describe(GeneratorAttributeSyntaxContext target)
    {
        var syntax = (PropertyDeclarationSyntax)target.TargetNode;
        var property = (IPropertySymbol)target.TargetSymbol;
        var type = property.Type.ToDisplayString(TypeFormat);
        if (property.IsStatic || !IsDefinition(syntax))
            return Problem.At(NotPartial, syntax, property.Name, property.Type.ToDisplayString());
        if (!DerivesFromPreferences(property.ContainingType))
            return Problem.At(OutsidePreferences, syntax, property.Name, property.ContainingType.Name);
        var nullable = target.SemanticModel.GetNullableContext(syntax.SpanStart).AnnotationsEnabled();
        return new Source(HintName(property), Write(syntax, property, type, nullable));
    }

    private static bool IsDefinition(PropertyDeclarationSyntax syntax)
    {
        return syntax.Modifiers.Any(SyntaxKind.PartialKeyword) && syntax.AccessorList is { } accessors &&
               accessors.Accessors.All(accessor => accessor.Body is null && accessor.ExpressionBody is null);
    }

    private static bool DerivesFromPreferences(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (current.Name == "Preferences" && current.ContainingNamespace.ToDisplayString() == "Winshell")
                return true;
        return false;
    }

    private static string HintName(IPropertySymbol property)
    {
        var name = new StringBuilder(property.ContainingType.ToDisplayString()).Append('.').Append(property.Name);
        for (var i = 0; i < name.Length; i++)
            if (!char.IsLetterOrDigit(name[i]) && name[i] != '.')
                name[i] = '_';
        return name.Append(".g.cs").ToString();
    }

    private static string Write(PropertyDeclarationSyntax syntax, IPropertySymbol property, string type, bool nullable)
    {
        var text = new StringBuilder(nullable ? "#nullable enable\n" : "#nullable disable\n");
        if (!property.ContainingNamespace.IsGlobalNamespace)
            text.Append("namespace ").Append(property.ContainingNamespace.ToDisplayString()).Append(";\n");
        var containers = new Stack<INamedTypeSymbol>();
        for (var container = property.ContainingType; container is not null; container = container.ContainingType)
            containers.Push(container);
        var indent = "";
        foreach (var container in containers)
        {
            text.Append(indent).Append("partial ").Append(Keyword(container)).Append(' ').Append(container.Name);
            if (container.TypeParameters.Length > 0)
                text.Append('<').Append(string.Join(", ", container.TypeParameters.Select(p => p.Name))).Append('>');
            text.Append('\n').Append(indent).Append("{\n");
            indent += "    ";
        }

        text.Append(indent).Append(string.Join(" ", syntax.Modifiers.Select(modifier => modifier.Text)))
            .Append(' ').Append(type).Append(' ').Append(property.Name).Append('\n')
            .Append(indent).Append("{\n");
        foreach (var accessor in syntax.AccessorList!.Accessors)
        {
            text.Append(indent).Append("    ");
            foreach (var modifier in accessor.Modifiers)
                text.Append(modifier.Text).Append(' ');
            text.Append(accessor.Keyword.Text)
                .Append(accessor.IsKind(SyntaxKind.GetAccessorDeclaration)
                    ? " => Read(field);\n"
                    : " => Set(ref field, value);\n");
        }

        text.Append(indent).Append("}\n");
        while (indent.Length > 0)
        {
            indent = indent.Substring(4);
            text.Append(indent).Append("}\n");
        }

        return text.ToString();
    }

    private static string Keyword(INamedTypeSymbol type)
    {
        return (type.IsRecord, type.TypeKind) switch
        {
            (true, TypeKind.Struct) => "record struct",
            (true, _) => "record",
            (_, TypeKind.Struct) => "struct",
            (_, TypeKind.Interface) => "interface",
            _ => "class"
        };
    }

    private abstract record Outcome;

    private sealed record Source(string HintName, string Text) : Outcome;

    private sealed record Problem(
        DiagnosticDescriptor Rule,
        string Path,
        TextSpan Span,
        LinePositionSpan Lines,
        string Property,
        string Detail) : Outcome
    {
        public static Problem At(DiagnosticDescriptor rule, PropertyDeclarationSyntax syntax, string property,
            string detail)
        {
            var location = syntax.Identifier.GetLocation();
            return new Problem(rule, location.SourceTree?.FilePath ?? "", location.SourceSpan,
                location.GetLineSpan().Span, property, detail);
        }
    }
}
