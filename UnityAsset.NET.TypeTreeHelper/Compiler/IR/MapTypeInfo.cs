using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.IR;

/// <summary>
/// A Unity map. It is resolved before <c>VectorTypeInfo</c> because a map also carries an Array child, and keeping it a
/// separate node is what lets the generated code treat maps - a key/value loop with string-keyed fast paths -
/// differently from plain vectors, without sniffing at the array element type everywhere.
/// </summary>
public readonly struct MapTypeInfo : IUnityTypeInfo
{
    public required TypeTreeRepr TypeTreeRepr { get; init; }
    public PairTypeInfo PairType { get; init; }
    public bool PairRequireAlign { get; init; }

    public TypeSyntax ToTypeSyntax()
    {
        return SyntaxFactory.ArrayType(PairType.ToTypeSyntax())
            .AddRankSpecifiers(
                SyntaxFactory.ArrayRankSpecifier(
                    SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(
                        SyntaxFactory.OmittedArraySizeExpression()
                    )
                )
            );
    }
}
