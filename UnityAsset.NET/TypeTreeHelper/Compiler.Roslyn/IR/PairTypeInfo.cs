using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Roslyn.IR;

public readonly struct PairTypeInfo : IUnityTypeInfo
{
    public IUnityTypeInfo Item1Type { get; init; }
    public IUnityTypeInfo Item2Type { get; init; }
    
    public bool Item1RequireAlign { get; init; }
    public bool Item2RequireAlign { get; init; }
    
    public TypeSyntax ToTypeSyntax()
    {
        return SyntaxFactory.GenericName("ValueTuple")
            .AddTypeArgumentListArguments(
                Item1Type.ToTypeSyntax(),
                Item2Type.ToTypeSyntax()
            );
    }
}