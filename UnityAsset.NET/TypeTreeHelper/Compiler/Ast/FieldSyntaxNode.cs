namespace UnityAsset.NET.TypeTreeHelper.Compiler.Ast;

public record FieldSyntaxNode(
    string OriginalName,
    string SanitizedName,
    bool RequiresAlign,
    TypeDeclarationNode FieldTypeNode,
    bool IsPresentInData
) : SyntaxNode(OriginalName, SanitizedName, RequiresAlign);
