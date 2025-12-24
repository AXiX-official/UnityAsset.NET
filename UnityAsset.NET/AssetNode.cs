namespace UnityAsset.NET;

public class AssetNode
{
    public required string Name { get; init; }
    public required string TypeName { get; init; }
    public object? Value { get; set; } = null;
    public List<AssetNode> Children { get; } = new();
}
