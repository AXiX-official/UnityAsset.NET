using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Roslyn.IR;

public readonly struct PredefinedTypeInfo : IUnityTypeInfo
{
    public TypeSyntax PredefinedTypeSyntax { get; init; }
    public TypeSyntax ToTypeSyntax() => PredefinedTypeSyntax;
}