using System.Text;
using UnityAsset.NET.TypeTreeHelper.Compiler.Ast;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Generator.Strategies;

public class FieldGenerationStrategy : IGenerationStrategy
{
    public void Apply(StringBuilder sb, ClassSyntaxNode classAst, GenerationOptions options)
    {
        sb.AppendLine($"    public string ClassName => \"{classAst.OriginalName}\";");
        
        foreach (var field in classAst.Fields)
        {
            sb.AppendLine();
            if (options.GenerateOriginalNameAttributes)
            {
                sb.AppendLine($"    [OriginalName(\"{field.OriginalName}\")]");
            }

            if (field.IsPresentInData)
            {
                sb.AppendLine($"    public {field.FieldTypeNode.DeclarationName} {field.SanitizedName} {{ get; }}");
            }
            else
            {
                sb.AppendLine($"    public {field.FieldTypeNode.DeclarationName}? {field.SanitizedName} => default;");
            }
        }
    }
}
