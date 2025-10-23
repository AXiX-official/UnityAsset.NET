using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Roslyn.IR;

public readonly struct UnityFieldInfo
{
    public string Name { get; init; }
    public bool RequireAlign { get; init; }
    public bool IsNullable { get; init; }
    public TypeSyntax DeclaredTypeSyntax { get; init; }
    public IUnityTypeInfo TypeInfo { get; init; }
}