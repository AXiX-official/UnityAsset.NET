using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.TypeTreeHelper;

namespace UnityAsset.NET.Extensions;

public static class TypeTreeNodeExtensions
{
    public static bool RequiresAlign(this TypeTreeNode node) => (node.MetaFlags & 0x4000) != 0;
    
    public static TypeTreeNode Parent(this TypeTreeNode current, List<TypeTreeNode> nodes)
    {
        if (current.Index == 0)
            throw new IndexOutOfRangeException();
        var nodesSpan = nodes.AsReadOnlySpan();
        for (int i = (int)current.Index - 1; i >= 0; i--)
        {
            if (nodesSpan[i].Level < current.Level)
                return nodesSpan[i];
        }
        throw new IndexOutOfRangeException();
    }
    
    public static List<TypeTreeNode> Children(this TypeTreeNode current, TypeTreeNode[] nodes)
    {
        if ((int)current.Index + 1 >= nodes.Length || nodes[(int)current.Index + 1].Level <= current.Level)
            return new();
        var children = new List<TypeTreeNode>();
        for (int i = (int)current.Index + 1; i < nodes.Length; i++)
        {
            if (nodes[i].Level <= current.Level)
                break;
            if (nodes[i].Level == current.Level + 1)
                children.Add(nodes[i]);
        }
        return children;
    }
    
    public static TypeTreeRepr ToTypeTreeRepr(this TypeTreeNode current, TypeTreeNode[] nodes)
    {
        if (String.IsNullOrEmpty(current.Type) || String.IsNullOrEmpty(current.Name))
            throw new Exception("Type/Name is empty");

        return TypeTreeRepr.Create
        (
            current.Name,
            current.Type,
            current.Children(nodes).Select(n => n.ToTypeTreeRepr(nodes)).ToArray(),
            current.RequiresAlign()
        );
    }
}