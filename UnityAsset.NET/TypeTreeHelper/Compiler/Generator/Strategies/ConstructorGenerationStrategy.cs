using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.Compiler.Ast;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Generator.Strategies;

public class ConstructorGenerationStrategy : IGenerationStrategy
{
    public void Apply(StringBuilder sb, ClassSyntaxNode classAst, GenerationOptions options)
    {
        sb.AppendLine();
        sb.AppendLine($"    public {classAst.ConcreteName}(IReader reader)");
        sb.AppendLine("    {");

        foreach (var field in classAst.Fields)
        {
            if (!field.IsPresentInData)
            {
                continue;
            }

            var readLogic = GetReadLogic(field.FieldTypeNode, "reader");
            sb.AppendLine($"        {field.SanitizedName} = {readLogic};");

            if (field.RequiresAlign)
            {
                sb.AppendLine("        reader.Align(4);");
            }
        }

        sb.AppendLine("    }");
    }

    private string GetReadLogic(TypeDeclarationNode node, string readerName)
    {
        return node switch
        {
            PrimitiveSyntaxNode p => GetPrimitiveReadLogic(p.OriginalName, readerName),
            PredefinedSyntaxNode p => $"new {p.DeclarationName}({readerName})",
            PairSyntaxNode p => $"{readerName}.ReadPairWithAlign<{p.KeyNode.DeclarationName}, {p.ValueNode.DeclarationName}>(r => {GetReadLogic(p.KeyNode, "r")}, r => {GetReadLogic(p.ValueNode, "r")}, {p.KeyNode.RequiresAlign.ToString().ToLower()}, {p.ValueNode.RequiresAlign.ToString().ToLower()})",
            ClassSyntaxNode c => $"new {c.ConcreteName}({readerName})",
            VectorSyntaxNode v => $"{readerName}.ReadListWithAlign<{v.ElementNode.DeclarationName}>({readerName}.ReadInt32(), r => {GetReadLogic(v.ElementNode, "r")}, {v.ElementNode.RequiresAlign.ToString().ToLower()})",
            MapSyntaxNode m => $"{readerName}.ReadMapWithAlign<{m.PairNode.KeyNode.DeclarationName}, {m.PairNode.ValueNode.DeclarationName}>({readerName}.ReadInt32(), r => {GetReadLogic(m.PairNode.KeyNode, "r")}, r => {GetReadLogic(m.PairNode.ValueNode, "r")}, {m.PairNode.KeyNode.RequiresAlign.ToString().ToLower()}, {m.PairNode.ValueNode.RequiresAlign.ToString().ToLower()})",
            _ => throw new Exception($"Read logic not implemented for {node.GetType().Name}")
        };
    }

    private string GetPrimitiveReadLogic(string originalName, string readerName)
    {
        return originalName switch
        {
            "bool" => $"{readerName}.ReadBoolean()",
            "sbyte" => $"{readerName}.ReadSByte()",
            "SInt8" => $"{readerName}.ReadInt8()",
            "byte" => $"{readerName}.ReadByte()",
            "UInt8" => $"{readerName}.ReadUInt8()",
            "Int16" or "short" or "SInt16" => $"{readerName}.ReadInt16()",
            "UInt16" or "unsigned short" => $"{readerName}.ReadUInt16()",
            "int" or "SInt32" => $"{readerName}.ReadInt32()",
            "uint" or "UInt32" or "unsigned int" or "Type*" => $"{readerName}.ReadUInt32()",
            "Int64" or "long long" or "SInt64" => $"{readerName}.ReadInt64()",
            "UInt64" or "unsigned long long" or "FileSize" => $"{readerName}.ReadUInt64()",
            "float" => $"{readerName}.ReadFloat()",
            "double" => $"{readerName}.ReadDouble()",
            "string" => $"{readerName}.ReadSizedString()",
            _ => throw new NotSupportedException($"Unknown primitive Unity type: {originalName}")
        };
    }
}
