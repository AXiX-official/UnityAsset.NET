namespace UnityAsset.NET.TypeTreeHelper;

public class TypeTreeNode
{
    public required string TypeName;

    public required string Name;

    public required int ByteSize;

    public required short Version;

    public required byte TypeFlags;

    public required uint MetaFlag;

    public required TypeTreeNode[] SubNodes;
    
    private int? _hash = null;

    public int Hash
    {
        get { 
            if (_hash != null)
                return _hash.Value;
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Name.GetDeterministicHashCode();
                hash = hash * 31 + TypeName.GetDeterministicHashCode();
                hash = hash * 31 + MetaFlag.GetHashCode();
            
                foreach (var child in SubNodes)
                {
                    hash = hash * 31 + child.Hash;
                }
            
                _hash = hash;
                return hash;
            }
        }
    }
    
    
    public bool RequiresAlign()
    {
        switch (TypeName)
        {
            case "string" :
            case "vector" :
                return InternalRequiresAlign() || SubNodes[0].RequiresAlign();
            case "map" :
                return InternalRequiresAlign() || SubNodes[0].SubNodes[1].RequiresAlign();
            default: return InternalRequiresAlign();
        }
    }
    
    private bool InternalRequiresAlign()
    {
        return (MetaFlag & 0x4000) != 0;
    }
}