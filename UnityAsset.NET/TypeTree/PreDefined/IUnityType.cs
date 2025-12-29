using System.Text;

namespace UnityAsset.NET.TypeTree.PreDefined;

public interface IUnityType
{
    public string ClassName { get; }

    public AssetNode? ToAssetNode(string name = "Base") => null;

    public string ToPlainText()
    {
        var root = ToAssetNode();
        if (root == null)
            return string.Empty;
        
        var sb = new StringBuilder();
        root.ToPlainText(sb);
        return sb.ToString();
    }
}