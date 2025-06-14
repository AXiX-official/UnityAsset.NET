using UnityAsset.NET.BundleFiles;

namespace UnityAsset.NET;

public class CabFileWrapper
{
    public ICabFile File { get; }
    public CabInfo Info { get; }

    public CabFileWrapper(ICabFile file, CabInfo info)
    {
        File = file;
        Info = info;
    }
}