using UnityAsset.NET.Enums;
using UnityAsset.NET.Files;

namespace UnityAsset.NET.IO;

public interface IReaderProvider : IFile
{
    public IReader CreateReader(Endianness endian = Endianness.BigEndian);
}