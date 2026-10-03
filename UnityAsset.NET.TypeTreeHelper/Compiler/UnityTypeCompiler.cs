using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityAsset.NET.TypeTreeHelper.Model;

namespace UnityAsset.NET.TypeTreeHelper.Compiler;

public class UnityTypeCompiler
{
    private readonly SemanticModelBuilder _semanticModelBuilder;
    private readonly RoslynTypeBuilder _builder = new();
    
    public UnityTypeCompiler()
    {
        _semanticModelBuilder = new SemanticModelBuilder();
    }

    public CompilationUnitSyntax Generate(IEnumerable<UnityTypeSchema> schemas)
    {
        var usingDirectives = SyntaxFactory.List([
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Text")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Collections.Generic")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.IO")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.Types")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.Types.PreDefined")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.Types.PreDefined.Types")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.Types.PreDefined.Interfaces"))
        ]);

        var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName("UnityAsset.NET.RuntimeTypes"));

        _builder.NamespaceDeclaration = namespaceDeclaration;
    
        foreach (var schema in schemas)
        {
            if (schema.Name == "MonoBehaviour")
                continue;
            _semanticModelBuilder.Build(schema, true);
        }

        _builder.Build(_semanticModelBuilder.DiscoveredTypes.Values);
        

        // Create the compilation unit (the whole file)
        var compilationUnit = SyntaxFactory.CompilationUnit()
            .WithUsings(usingDirectives)
            .AddMembers(_builder.NamespaceDeclaration);

        // The interfaces declare optional members with "?", so the classes that implement them are read in the same
        // nullable context.
        var nullableContext = SyntaxFactory.TriviaList(
            SyntaxFactory.Trivia(SyntaxFactory.NullableDirectiveTrivia(SyntaxFactory.Token(SyntaxKind.EnableKeyword), true)),
            SyntaxFactory.CarriageReturnLineFeed,
            SyntaxFactory.CarriageReturnLineFeed);

        var firstToken = compilationUnit.GetFirstToken(includeZeroWidth: true);
        return compilationUnit.ReplaceToken(firstToken, firstToken.WithLeadingTrivia(nullableContext));
    }
}