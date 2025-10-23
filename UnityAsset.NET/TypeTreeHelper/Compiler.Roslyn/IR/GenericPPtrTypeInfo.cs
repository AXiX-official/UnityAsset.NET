using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Roslyn.IR;

public readonly struct GenericPPtrTypeInfo : IUnityTypeInfo
{
    public TypeSyntax GenericTypeSyntax { get; init; }

    public TypeSyntax ToTypeSyntax()
    {
        return SyntaxFactory.GenericName("PPtr")
            .AddTypeArgumentListArguments(
                GenericTypeSyntax
            );
    }
}