using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Roslyn.IR;

public readonly struct VectorTypeInfo : IUnityTypeInfo
{
    public IUnityTypeInfo ElementType { get; init; }
    public bool ElementRequireAlign { get; init; }

    public TypeSyntax ToTypeSyntax()
    {
        return SyntaxFactory.GenericName("List")
            .AddTypeArgumentListArguments(ElementType.ToTypeSyntax());
    }
}