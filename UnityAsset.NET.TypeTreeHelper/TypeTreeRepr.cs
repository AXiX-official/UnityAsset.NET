using System.Collections.Concurrent;

namespace UnityAsset.NET.TypeTreeHelper;

public class TypeTreeRepr : IEquatable<TypeTreeRepr>
{
    public static ConcurrentDictionary<TypeTreeRepr, TypeTreeRepr> Cache = new();
    
    public readonly string Name;
    public readonly string TypeName;
    public readonly TypeTreeRepr[] SubNodes;
    
    public bool RequiresAlign { get; }
    
    public int Hash { get; }

    private TypeTreeRepr(string name, string typeName, TypeTreeRepr[] subNodes, bool requiresAlign)
    {
        Name = name;
        TypeName = typeName;
        SubNodes = subNodes;
        RequiresAlign = requiresAlign;
        
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

    public static bool operator ==(TypeTreeRepr? left, TypeTreeRepr? right)
    {
        if (left is null) 
            return right is null; 
        return left.Equals(right);
    }

    public static bool operator !=(TypeTreeRepr? left, TypeTreeRepr? right)
    {
        return !(left == right);
    }
}