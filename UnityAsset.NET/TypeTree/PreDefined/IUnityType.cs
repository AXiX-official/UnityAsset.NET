using System.Text;

namespace UnityAsset.NET.TypeTree.PreDefined;

public interface IUnityType
{
    public string ClassName { get; }

    public AssetNode? ToAssetNode(string name = "Base") => null;
    
    public string ToPlainText() => string.Empty;
}