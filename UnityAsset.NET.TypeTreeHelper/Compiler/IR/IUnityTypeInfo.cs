using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.IR;

public interface IUnityTypeInfo
{
    TypeTreeRepr TypeTreeRepr { get; }
    TypeSyntax ToTypeSyntax();
}