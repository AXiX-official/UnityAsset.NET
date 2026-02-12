using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.IR;

public readonly struct VectorTypeInfo : IUnityTypeInfo
{
    public required TypeTreeRepr TypeTreeRepr { get; init; }
    //public required TypeTreeNode DataTypeTreeNode { get; init; }
    //public required TypeTreeNode SizeTypeTreeNode { get; init; }
    public IUnityTypeInfo ElementType { get; init; }
    public bool ElementRequireAlign { get; init; }

    public TypeSyntax ToTypeSyntax()
    {
        return SyntaxFactory.GenericName("List")
            .AddTypeArgumentListArguments(ElementType.ToTypeSyntax());
    }
}