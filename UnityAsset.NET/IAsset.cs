using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.Types.PreDefined;

namespace UnityAsset.NET;

public interface IAsset
{
    public IUnityAsset Value { get; }
    public string TypeName { get; }
    public string Name { get; }
    public long Size { get; }
    public long PathId { get; }
    public string Container { get; }
    public SerializedType Type { get; }

    void Write(IWriter writer);
}