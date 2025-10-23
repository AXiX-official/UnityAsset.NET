using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Roslyn.IR;

public readonly struct PrimitiveTypeInfo : IUnityTypeInfo
{
    public string OriginalTypeName { get; init; }
    public TypeSyntax PrimitiveSyntax { get; init; }
    public TypeSyntax ToTypeSyntax() => PrimitiveSyntax;
}