using UnityAsset.NET.Enums;

namespace UnityAsset.NET.IO;

public interface IReaderProvider
{
    public IReader CreateReader(Endianness endian = Endianness.BigEndian);
}