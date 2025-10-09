namespace UnityAsset.NET.TypeTreeHelper.Compiler.Ast;

public record PrimitiveSyntaxNode(
    string OriginalName,
    string SanitizedName,
    bool RequiresAlign,
    string DeclarationName
) : TypeDeclarationNode(OriginalName, SanitizedName, RequiresAlign, DeclarationName);
