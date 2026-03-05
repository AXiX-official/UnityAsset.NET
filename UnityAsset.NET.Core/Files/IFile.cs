using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files
{
    public interface IFile
    {
        public ulong WriteBytes(IWriter writer);
    }
}
