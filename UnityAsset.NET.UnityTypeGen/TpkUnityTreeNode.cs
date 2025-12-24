namespace UnityAsset.NET.UnityTypeGen;

public class TpkUnityTreeNode
{
    public required int Index;
    
    public required string TypeName;

    public required string Name;

    public required int ByteSize;

    public required short Version;

    public required byte TypeFlags;

    public required uint MetaFlag;

    public required TpkUnityTreeNode[] SubNodes;
}