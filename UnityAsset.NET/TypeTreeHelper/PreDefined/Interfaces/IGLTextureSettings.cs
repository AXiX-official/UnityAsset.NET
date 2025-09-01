using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;

public interface IGLTextureSettings : IPreDefinedInterface
{
    public Int32 m_FilterMode { get; }
    public Int32 m_Aniso { get; }
    public float m_MipBias { get; }
    public Int32 m_WrapU { get; }
    public Int32 m_WrapV { get; }
    public Int32 m_WrapW { get; }
}