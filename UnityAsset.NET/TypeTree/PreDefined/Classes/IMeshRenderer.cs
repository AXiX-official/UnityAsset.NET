namespace UnityAsset.NET.TypeTree.PreDefined.Interfaces;

public partial interface IMeshRenderer : IComponent, Renderer
{
    /*public IMesh? TryGetMesh(AssetManager mgr)
    {
        if (m_GameObject.TryGet(mgr, out var gameObject))
        {
            if (gameObject.m_MeshFilter is not null)
            {
                if (gameObject.m_MeshFilter.m_Mesh.TryGet(mgr, out var mesh))
                {
                    return mesh;
                } 
            }
        }
        return null;
    }*/
}