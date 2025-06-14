using System.Collections.Specialized;
using UnityAsset.NET.IO;
using UnityAsset.NET.SerializedFiles;

namespace UnityAsset.NET;

public class Asset
{ 
    public AssetFileInfo Info;
    public HeapDataBuffer RawData;
    public NodeData NodeData;

    public Asset(AssetFileInfo info, HeapDataBuffer hdb)
    {
        Info = info;
        RawData = hdb;
        var nodeDataList = new List<NodeData>();
        for (int i = 0; i < info.Type.Nodes.Count; i++)
        {
            var typeTreeNode = info.Type.Nodes[i];
            var node = new NodeData(typeTreeNode); 
            nodeDataList.Add(node);
        }
        var parent = nodeDataList[0];
        for (int i = 1; i < nodeDataList.Count; i++)
        {
            while (nodeDataList[i].Level <= parent.Level)
            {
                parent = parent.Parent;
            }
            nodeDataList[i].Parent = parent;
            parent.Children ??= new ();
            parent.Children.Add(nodeDataList[i]);
            parent = nodeDataList[i];
        }
        for (int i = 0; i < nodeDataList.Count; i++)
        {
            var node = nodeDataList[i];
            node.Value = NodeData.ReadValue(nodeDataList, hdb, ref i);
        }
        NodeData = nodeDataList[0];
    }
}