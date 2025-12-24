using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.IR;

public readonly struct MapTypeInfo : IUnityTypeInfo
{
    public required TypeTreeNode TypeTreeNode { get; init; }
    public PairTypeInfo PairType { get; init; }
    public bool PairRequireAlign { get; init; }

    public TypeSyntax ToTypeSyntax()
    {
        return SyntaxFactory.GenericName("List")
            .AddTypeArgumentListArguments(PairType.ToTypeSyntax());
    }
}