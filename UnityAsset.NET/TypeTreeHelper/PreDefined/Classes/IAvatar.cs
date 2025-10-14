using UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface IAvatar : INamedAsset
{
    public UInt32 m_AvatarSize { get; }
    public IAvatarConstant m_Avatar { get; }
    public List<KeyValuePair<UInt32, string>> m_TOS { get; }
    //public IUnityType m_HumanDescription { get; }
}