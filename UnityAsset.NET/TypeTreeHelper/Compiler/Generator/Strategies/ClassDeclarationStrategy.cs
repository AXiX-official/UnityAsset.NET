using System.Text;
using UnityAsset.NET.TypeTreeHelper.Compiler.Ast;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Generator.Strategies;

public class ClassDeclarationStrategy : IGenerationStrategy
{
    public void Apply(StringBuilder sb, ClassSyntaxNode classAst, GenerationOptions options)
    {
        sb.AppendLine($"public class {classAst.ConcreteName} : {classAst.InterfaceName}");
        sb.AppendLine("{");
    }
}

public class ClassClosingStrategy : IGenerationStrategy
{
    public void Apply(StringBuilder sb, ClassSyntaxNode classAst, GenerationOptions options)
    {
        sb.AppendLine("}");
    }
}
