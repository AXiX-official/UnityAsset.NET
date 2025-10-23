using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Roslyn.IR;

public interface IUnityTypeInfo
{
    TypeSyntax ToTypeSyntax();
}