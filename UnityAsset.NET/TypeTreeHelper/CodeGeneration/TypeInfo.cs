namespace UnityAsset.NET.TypeTreeHelper.CodeGeneration;

public enum TypeKind : byte
{
    Primitive,
    Vector,
    Map,
    Predefined,
    Complex
}

public record BaseTypeInfo(
    TypeKind Kind,
    bool RequiresAlign,
    bool RequiresGeneration,
    string OriginalName,
    string DeclarationName,
    string InterfaceName,
    string ReadLogic
);

public record PrimitiveTypeInfo(
    bool RequiresAlign,
    string OriginalName
) : BaseTypeInfo(TypeKind.Primitive, RequiresAlign, false, OriginalName,
    Helper.GetCSharpPrimitiveType(OriginalName),
    string.Empty,
    Helper.GetPrimitiveReadLogic(OriginalName));

public record VectorTypeInfo(
    bool RequiresAlign,
    string OriginalName,
    string ArrayType,
    string ArrayName,
    string SizeType,
    string SizeName,
    BaseTypeInfo ElementTypeInfo,
    string ElementName,
    string ReadLogic
) : BaseTypeInfo(TypeKind.Vector, RequiresAlign, false, OriginalName,
    $"List<{ElementTypeInfo.DeclarationName}>",
    string.Empty,
    ReadLogic);


public record MapTypeInfo(
    bool RequiresAlign,
    string OriginalName,
    string PairType,
    string PairName,
    BaseTypeInfo KeyTypeInfo,
    string KeyName,
    BaseTypeInfo ValueTypeInfo,
    string ValueName,
    string ReadLogic
) : BaseTypeInfo(TypeKind.Map, RequiresAlign, false, OriginalName,
    $"List<KeyValuePair<{KeyTypeInfo.DeclarationName}, {ValueTypeInfo.DeclarationName}>>",
    string.Empty,
    ReadLogic);

    
public record PredefinedTypeInfo(
    bool RequiresAlign,
    string OriginalName,
    string DeclarationName,
    string ReadLogic
    ) : BaseTypeInfo(TypeKind.Predefined, RequiresAlign, false, OriginalName, DeclarationName, string.Empty, ReadLogic);

public record ComplexTypeInfo(
    bool RequiresAlign,
    string OriginalName,
    string DeclarationName,
    string InterfaceName,
    string ConcreteName,
    (string, BaseTypeInfo)[] Fields
    ) : BaseTypeInfo(TypeKind.Complex, RequiresAlign, true, OriginalName, DeclarationName, InterfaceName, $"new {ConcreteName}(reader)");


