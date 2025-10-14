namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface IMonoScript : INamedAsset
{
    public string m_ClassName { get; }

    public string m_Namespace { get; }

    public string m_AssemblyName { get; }
}