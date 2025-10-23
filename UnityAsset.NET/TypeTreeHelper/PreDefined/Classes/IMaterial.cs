using UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface IMaterial : INamedAsset
{
    public PPtr<IShader> m_Shader { get; }
    public List<string>? m_ValidKeywords { get => null; }

    public List<string>? m_InvalidKeywords { get => null; }
    public string? m_ShaderKeywords { get => null; }
    public UInt32 m_LightmapFlags { get; }
    public bool m_EnableInstancingVariants { get; }
    public bool m_DoubleSidedGI { get; }

    public Int32 m_CustomRenderQueue { get; }
    public List<(string, string)> stringTagMap { get; }
    public List<string> disabledShaderPasses { get; }

    public IUnityPropertySheet m_SavedProperties { get; }

    public List<BuildTextureStackReference>? m_BuildTextureStacks { get => null; }
}