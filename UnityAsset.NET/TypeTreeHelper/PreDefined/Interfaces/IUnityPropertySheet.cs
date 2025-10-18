namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;

public interface IUnityPropertySheet : IPreDefinedInterface
{
    public List<(string, IUnityTexEnv)> m_TexEnvs { get; }

    public List<(string, Int32)>? m_Ints { get; }

    public List<(string, float)> m_Floats { get; }

    public List<(string, IUnityType)> m_Colors { get; }
}