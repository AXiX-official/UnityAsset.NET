namespace UnityAsset.NET.TypeTreeHelper.Compiler.Ast;

public record ClassSyntaxNode(
    string OriginalName,
    string SanitizedName,
    bool RequiresAlign,
    string InterfaceName,
    string ConcreteName,
    FieldSyntaxNode[] Fields
) : TypeDeclarationNode(OriginalName, SanitizedName, RequiresAlign, InterfaceName);
