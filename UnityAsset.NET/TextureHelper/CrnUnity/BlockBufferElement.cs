// based on https://github.com/nesrak1/AssetsTools.NET/tree/dev/AssetsTools.NET.Texture/TextureDecoders
namespace UnityAsset.NET.TextureHelper.CrnUnity;
internal struct BlockBufferElement
{
    public ushort EndpointReference;
    public ushort ColorEndpointIndex;
    public ushort Alpha0EndpointIndex;
    public ushort Alpha1EndpointIndex;
}
