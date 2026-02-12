using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.IR;

public readonly struct PredefinedTypeInfo : IUnityTypeInfo
{
    public required TypeTreeRepr TypeTreeRepr { get; init; }
    public TypeSyntax PredefinedTypeSyntax { get; init; }
    public TypeSyntax ToTypeSyntax() => PredefinedTypeSyntax;
}