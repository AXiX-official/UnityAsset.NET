namespace UnityAsset.NET.Types.PreDefined.Interfaces;

public partial interface ISkinnedMeshRenderer
{
    public IMesh? TryGetMesh(IUnitySession session)
    {
        if (m_Mesh.TryGet(session, out var mesh))
        {
            return mesh;
        }
        return null;
    }
}
