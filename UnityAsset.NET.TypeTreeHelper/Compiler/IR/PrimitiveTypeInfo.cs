using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.IR;

public readonly struct PrimitiveTypeInfo : IUnityTypeInfo
{
    public required TypeTreeRepr TypeTreeRepr { get; init; }
    public string OriginalTypeName { get; init; }
    public TypeSyntax PrimitiveSyntax { get; init; }
    public TypeSyntax ToTypeSyntax() => PrimitiveSyntax;
}