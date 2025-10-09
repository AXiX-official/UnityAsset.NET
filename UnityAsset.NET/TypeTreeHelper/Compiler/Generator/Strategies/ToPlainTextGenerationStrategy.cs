using System.Text;
using UnityAsset.NET.TypeTreeHelper.Compiler.Ast;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Generator.Strategies;

public class ToPlainTextGenerationStrategy : IGenerationStrategy
{
    public void Apply(StringBuilder sb, ClassSyntaxNode classAst, GenerationOptions options)
    {
        sb.AppendLine();
        sb.AppendLine("    public StringBuilder ToPlainText(string name = \"Base\", StringBuilder? sb = null, string indent = \"\")");
        sb.AppendLine("    {");
        sb.AppendLine("        sb ??= new StringBuilder();");
        sb.AppendLine($"        sb.AppendLine($\"{{indent}}{{ClassName}} {{name}}\");");
        sb.AppendLine($"        var childIndent = indent + \"    \";");

        foreach (var field in classAst.Fields)
        {
            GenerateFieldToPlainText(sb, field, "        ");
        }
        
        sb.AppendLine("        return sb;");
        sb.AppendLine("    }");
    }

    private void GenerateFieldToPlainText(StringBuilder sb, FieldSyntaxNode field, string baseIndent)
    {
        if (!field.IsPresentInData)
        {
            return;
        }
        
        var valueAccess = $"this.{field.SanitizedName}";
        AppendRecursive(sb, field.FieldTypeNode, field.OriginalName, valueAccess, baseIndent);
    }

    private void AppendRecursive(StringBuilder sb, TypeDeclarationNode typeNode, string name, string valueAccess, string indent)
    {
        switch (typeNode)
        {
            case PrimitiveSyntaxNode p:
                var quote = p.OriginalName == "string" ? "\\\"" : "";
                sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}{p.OriginalName} {name} = {quote}{{{valueAccess}}}{quote}\");");
                break;

            case VectorSyntaxNode v:
                sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}{v.OriginalName} {name}\");");
                sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}    Array Array\");");
                sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}    int size =  {{(uint){valueAccess}.Count}}\");");
                sb.AppendLine($"{indent}for (int i{name} = 0; i{name} < {valueAccess}.Count; i{name}++)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    var {name}childIndentBackUp = childIndent;");
                sb.AppendLine($"{indent}    childIndent = $\"{{childIndent}}        \";");
                sb.AppendLine($"{indent}    sb.AppendLine($\"{{childIndent}}[{{i{name}}}]\");");
                AppendRecursive(sb, v.ElementNode, "data", $"{valueAccess}[i{name}]", $"{indent}    ");
                sb.AppendLine($"{indent}    childIndent = {name}childIndentBackUp;");
                sb.AppendLine($"{indent}}}");
                break;

            case MapSyntaxNode m:
                sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}{{m.OriginalName}} {name}\");");
                sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}    Array Array)\";");
                sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}    int size =  {{(uint){valueAccess}.Count}}\");");
                sb.AppendLine($"{indent}for (int i{name} = 0; i{name} < {valueAccess}.Count; i{name}++)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    var {name}childIndentBackUp = childIndent;");
                sb.AppendLine($"{indent}    childIndent = $\"{{childIndent}}        \";");
                sb.AppendLine($"{indent}    sb.AppendLine($\"{{childIndent}}[{{i{name}}}]\");");
                AppendRecursive(sb, m.PairNode, "data", $"{valueAccess}[i{name}]", $"{indent}    ");
                sb.AppendLine($"{indent}    childIndent = {name}childIndentBackUp;");
                sb.AppendLine($"{indent}}}");
                break;

            case PairSyntaxNode p:
                sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}pair {name}\");");
                AppendRecursive(sb, p.KeyNode, "first", $"{valueAccess}.Key", $"{indent}    ");
                AppendRecursive(sb, p.ValueNode, "second", $"{valueAccess}.Value", $"{indent}    ");
                break;

            case ClassSyntaxNode c:
            case PredefinedSyntaxNode pd:
                sb.AppendLine($"{indent}{valueAccess}?.ToPlainText(\"{name}\", sb, childIndent);");
                break;
        }
    }
}
