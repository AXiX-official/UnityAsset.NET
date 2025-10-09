namespace UnityAsset.NET.TypeTreeHelper.Compiler.Ast;

public record VectorSyntaxNode(
    string OriginalName,
    string SanitizedName,
    bool RequiresAlign,
    TypeDeclarationNode ElementNode
) : TypeDeclarationNode(OriginalName, SanitizedName, RequiresAlign, $"List<{ElementNode.DeclarationName}>");
