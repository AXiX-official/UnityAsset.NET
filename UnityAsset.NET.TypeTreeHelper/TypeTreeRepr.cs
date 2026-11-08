using System.Collections.Concurrent;

namespace UnityAsset.NET.TypeTreeHelper;

public enum NodeKind : byte
{
    SInt8,
    UInt8,
    Char,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Single,
    Double,
    Boolean,
    String,
    Map,
    TypelessData,

    /// <summary>A vector: one subnode holding the length and the element schema.</summary>
    Array,

    /// <summary>Everything else: a structure whose subnodes are its members.</summary>
    Class,
}

public class TypeTreeRepr : IEquatable<TypeTreeRepr>
{
    public static ConcurrentDictionary<TypeTreeRepr, TypeTreeRepr> Cache = new();
    
    public readonly string Name;
    public readonly string TypeName;
    public readonly TypeTreeRepr[] SubNodes;
    
    public bool RequiresAlign { get; }
    public bool IsNamed { get; }
    public int Hash { get; }
    public NodeKind Kind { get; }

    private TypeTreeRepr(string name, string typeName, TypeTreeRepr[] subNodes, bool requiresAlign)
    {
        Name = name;
        TypeName = typeName;
        SubNodes = subNodes;
        RequiresAlign = requiresAlign;
        IsNamed = subNodes.Any(x => x.Name == "m_Name");
        Kind = KindOf(typeName, subNodes);

        if (TypeName == "map")
            RequiresAlign |= SubNodes[0].SubNodes[1].RequiresAlign;
        else if (SubNodes.Length == 1 && SubNodes[0].TypeName == "Array")
            RequiresAlign |= SubNodes[0].RequiresAlign;

        Hash = 17;
        Hash = Hash * 31 + TypeName.GetDeterministicHashCode();
        foreach (var child in SubNodes)
            Hash = Hash * 31 + child.GetHashCode();
        Hash = Hash * 31 + RequiresAlign.GetHashCode();
    }

    private static NodeKind KindOf(string typeName, TypeTreeRepr[] subNodes) => typeName switch
    {
        "SInt8" => NodeKind.SInt8,
        "UInt8" => NodeKind.UInt8,
        "char" => NodeKind.Char,
        "short" or "SInt16" => NodeKind.Int16,
        "UInt16" or "unsigned short" => NodeKind.UInt16,
        "int" or "SInt32" => NodeKind.Int32,
        "UInt32" or "unsigned int" or "Type*" => NodeKind.UInt32,
        "long long" or "SInt64" => NodeKind.Int64,
        "UInt64" or "unsigned long long" or "FileSize" => NodeKind.UInt64,
        "float" => NodeKind.Single,
        "double" => NodeKind.Double,
        "bool" => NodeKind.Boolean,
        "string" => NodeKind.String,
        "map" => NodeKind.Map,
        "TypelessData" => NodeKind.TypelessData,
        _ => subNodes.Length == 1 && subNodes[0].TypeName == "Array" ? NodeKind.Array : NodeKind.Class,
    };

    public static TypeTreeRepr Create(string name, string typeName, TypeTreeRepr[] subNodes, bool requiresAlign)
    {
        var candidate = new TypeTreeRepr(name, typeName, subNodes, requiresAlign);
        return Cache.GetOrAdd(candidate, key => key);
    }
    
    public override int GetHashCode() => Hash * 31 + Name.GetDeterministicHashCode();
    
    public IEnumerable<(TypeTreeRepr Node, byte Level)> Traverse(byte level = 0)
    {
        yield return (this, level);

        foreach (var child in SubNodes)
        {
            foreach (var item in child.Traverse((byte)(level + 1)))
                yield return item;
        }
    }

    public override bool Equals(object? obj) => Equals(obj as TypeTreeRepr);

    public bool Equals(TypeTreeRepr? other)
    {
        if (ReferenceEquals(this, other)) 
            return true;
        
        if (other is null) 
            return false;
        
        if (GetHashCode() != other.GetHashCode()) 
            return false;
        
        if (!string.Equals(Name, other.Name, StringComparison.Ordinal)) 
            return false;
        
        if (!string.Equals(TypeName, other.TypeName, StringComparison.Ordinal)) 
            return false;
        
        if (RequiresAlign != other.RequiresAlign)
            return false;
        
        if (SubNodes.Length != other.SubNodes.Length) 
            return false;

        for (int i = 0; i < SubNodes.Length; i++)
        {
            if (!Equals(SubNodes[i], other.SubNodes[i])) 
                return false;
        } 
        
        return true;
    }
}