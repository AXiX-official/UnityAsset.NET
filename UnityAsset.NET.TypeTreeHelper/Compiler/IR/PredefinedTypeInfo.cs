using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.IR;

public readonly struct PredefinedTypeInfo : IUnityTypeInfo
{
    public required TypeTreeNode TypeTreeNode { get; init; }
    public TypeSyntax PredefinedTypeSyntax { get; init; }
    public TypeSyntax ToTypeSyntax() => PredefinedTypeSyntax;
}