namespace UnityAsset.NET.TypeTreeHelper.Compiler.Ast;

public record MapSyntaxNode(
    string OriginalName,
    string SanitizedName,
    bool RequiresAlign,
    PairSyntaxNode PairNode
) : TypeDeclarationNode(OriginalName, SanitizedName, RequiresAlign, $"List<{PairNode.DeclarationName}>");
