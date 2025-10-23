using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using UnityAsset.NET.Files.SerializedFiles;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Roslyn
{
    public class UnityRoslynCompiler
    {
        private readonly SemanticModelBuilder _semanticModelBuilder = new();
        private readonly RoslynTypeBuilder _builder = new();

        public CompilationUnitSyntax Generate(List<SerializedType> serializedTypes)
        {
            var usingDirectives = SyntaxFactory.List([
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Text")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Collections.Generic")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.IO")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.TypeTreeHelper")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.TypeTreeHelper.PreDefined")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.TypeTreeHelper.PreDefined.Classes")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.TypeTreeHelper.PreDefined.Types")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces"))
            ]);

            var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName("UnityAsset.NET.RuntimeType"));

            _builder.NamespaceDeclaration = namespaceDeclaration;

            var rootTypeHash = new HashSet<Hash128>();
        
            foreach (var serializedType in serializedTypes)
            {
                if (serializedType.Nodes.Count == 0 || serializedType.Nodes[0].Level != 0) continue;
            
                if (rootTypeHash.Contains(serializedType.TypeHash)) continue;

                var rootNode = serializedType.Nodes[0];
                var classTypeInfo = _semanticModelBuilder.Build(rootNode, serializedType.Nodes);
                if (classTypeInfo == null) continue;
                rootTypeHash.Add(serializedType.TypeHash);
            }

            _builder.Build(_semanticModelBuilder.DiscoveredTypes.Values);
            

            // Create the compilation unit (the whole file)
            var compilationUnit = SyntaxFactory.CompilationUnit()
                .WithUsings(usingDirectives)
                .AddMembers(_builder.NamespaceDeclaration);
            
            return compilationUnit;
        }
        
        // TODO: Add in-memory compilation logic here
    }
}