namespace UnityAsset.NET.Types.PreDefined;

public interface INamedObject : IUnityAsset
{
    public string m_Name
    {
        get => throw new NotSupportedException("This member has no implementation.");
        set => throw new NotSupportedException("Clone an editable implementation before changing its name.");
    }
}
