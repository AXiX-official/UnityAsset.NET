namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;

public interface IUnityPropertySheet : IPreDefinedInterface
{
    public List<KeyValuePair<string, IUnityTexEnv>> m_TexEnvs { get; }

    public List<KeyValuePair<string, Int32>>? m_Ints { get; }

    public List<KeyValuePair<string, float>> m_Floats { get; }

    public List<KeyValuePair<string, IUnityType>> m_Colors { get; }
}