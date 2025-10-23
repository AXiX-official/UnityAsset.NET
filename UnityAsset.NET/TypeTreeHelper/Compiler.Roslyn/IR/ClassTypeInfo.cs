using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Roslyn.IR;

public class ClassTypeInfo : IUnityTypeInfo
{
    public required string Name;
    public required string GeneratedClassName { get; init; }
    public required string InterfaceName { get; init; }
    public required List<UnityFieldInfo> Fields { get; init; }

    public TypeSyntax ToTypeSyntax() => SyntaxFactory.ParseTypeName(InterfaceName);
    public TypeSyntax ToConcreteTypeSyntax() => SyntaxFactory.ParseTypeName(GeneratedClassName);
}