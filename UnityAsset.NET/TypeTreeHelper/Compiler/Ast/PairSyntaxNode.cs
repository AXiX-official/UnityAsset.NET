namespace UnityAsset.NET.TypeTreeHelper.Compiler.Ast;

public record PairSyntaxNode(
    string OriginalName,
    string SanitizedName,
    bool RequiresAlign,
    TypeDeclarationNode KeyNode,
    TypeDeclarationNode ValueNode
) : TypeDeclarationNode(OriginalName, SanitizedName, RequiresAlign, $"KeyValuePair<{KeyNode.DeclarationName}, {ValueNode.DeclarationName}>");
