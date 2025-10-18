using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface IAssetBundle : INamedAsset
{
    public List<PPtr<Types.Object>> m_PreloadTable { get; }
    public List<(string, AssetInfo)> m_Container { get; }
    //public AssetInfo m_MainAsset { get; }
    //public UInt32 m_RuntimeCompatibility { get; }
    //public string m_AssetBundleName { get; }
    public List<string>? m_Dependencies { get; }
    //public bool m_IsStreamedSceneAssetBundle { get; }
    //public Int32 m_ExplicitDataLayout { get; }
    //public Int32 m_PathFlags { get; }
    //public List<pair> m_SceneHashes { get; }
}