using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.IR;

public interface IUnityTypeInfo
{
    TypeTreeNode TypeTreeNode { get; }
    TypeSyntax ToTypeSyntax();
}