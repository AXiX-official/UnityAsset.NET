
namespace UnityAsset.NET.TypeTreeHelper.Compiler.Ast;

public enum TypeKind
{
    Complex,
    Primitive,
    Vector,
    Map,
    Pair
}

public abstract record SyntaxNode(
    string OriginalName,
    string SanitizedName,
    bool RequiresAlign
);

public abstract record TypeDeclarationNode(
    string OriginalName,
    string SanitizedName,
    bool RequiresAlign,
    string DeclarationName
) : SyntaxNode(OriginalName, SanitizedName, RequiresAlign);
