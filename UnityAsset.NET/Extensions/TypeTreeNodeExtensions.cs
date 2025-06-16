using UnityAsset.NET.Files.SerializedFiles;

namespace UnityAsset.NET.Extensions;

public static class TypeTreeNodeExtensions
{
    public static bool RequiresAlign(this TypeTreeNode node) => (node.MetaFlags & 0x4000) != 0;
    
    public static TypeTreeNode? Parent(this TypeTreeNode current, List<TypeTreeNode> nodes)
    {
        if (current.Index == 0)
            return null;
        var nodesSpan = nodes.AsReadOnlySpan();
        for (int i = (int)current.Index - 1; i >= 0; i--)
        {
            if (nodesSpan[i].Level < current.Level)
                return nodesSpan[i];
        }
        return null;
    }
    
    public static List<TypeTreeNode>? Children(this TypeTreeNode current, List<TypeTreeNode> nodes)
    {
        var nodesSpan = nodes.AsReadOnlySpan();
        if ((int)current.Index + 1 >= nodesSpan.Length || nodesSpan[(int)current.Index + 1].Level <= current.Level)
            return null;
        var children = new List<TypeTreeNode>();
        for (int i = (int)current.Index + 1; i < nodesSpan.Length; i++)
        {
            if (nodesSpan[i].Level <= current.Level)
                break;
            if (nodesSpan[i].Level == current.Level + 1)
                children.Add(nodesSpan[i]);
        }
        return children;
    }
}