namespace UnityAsset.NET.TypeTree.PreDefined.Interfaces;

public partial interface ISkinnedMeshRenderer : IComponent, Renderer
{
    public IMesh? TryGetMesh(AssetManager mgr)
    {
        if (m_Mesh.TryGet(mgr, out var mesh))
        {
            return mesh;
        }
        return null;
    }
}