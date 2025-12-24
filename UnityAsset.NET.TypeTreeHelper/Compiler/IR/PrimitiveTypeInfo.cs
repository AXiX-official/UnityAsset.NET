using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.IR;

public readonly struct PrimitiveTypeInfo : IUnityTypeInfo
{
    public required TypeTreeNode TypeTreeNode { get; init; }
    public string OriginalTypeName { get; init; }
    public TypeSyntax PrimitiveSyntax { get; init; }
    public TypeSyntax ToTypeSyntax() => PrimitiveSyntax;
}