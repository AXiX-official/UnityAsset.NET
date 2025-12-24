using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler;

public class UnityTypeCompiler
{
    private readonly SemanticModelBuilder _semanticModelBuilder;
    private readonly RoslynTypeBuilder _builder = new();
    
    public UnityTypeCompiler(Dictionary<string, Type> preDefinedInterfaceMap)
    {
        _semanticModelBuilder = new SemanticModelBuilder(preDefinedInterfaceMap);
    }

    public CompilationUnitSyntax Generate(IEnumerable<TypeTreeNode> rootNodes)
    {
        var usingDirectives = SyntaxFactory.List([
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Text")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Collections.Generic")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.IO")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.TypeTree")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.TypeTree.PreDefined")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.TypeTree.PreDefined.Types")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.TypeTree.PreDefined.Interfaces"))
        ]);

        var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName("UnityAsset.NET.RuntimeType"));

        _builder.NamespaceDeclaration = namespaceDeclaration;
    
        foreach (var rootNode in rootNodes)
        {
            _semanticModelBuilder.Build(rootNode);
        }

        _builder.Build(_semanticModelBuilder.DiscoveredTypes.Values);
        

        // Create the compilation unit (the whole file)
        var compilationUnit = SyntaxFactory.CompilationUnit()
            .WithUsings(usingDirectives)
            .AddMembers(_builder.NamespaceDeclaration);
        
        return compilationUnit;
    }
}