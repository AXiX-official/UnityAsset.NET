using System.Text;
using UnityAsset.NET.TypeTreeHelper.Compiler.Ast;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Generator.Strategies;

public interface IGenerationStrategy
{
    void Apply(StringBuilder sb, ClassSyntaxNode classAst, GenerationOptions options);
}
