using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;

namespace UnityAsset.NET.Editing;

internal sealed class MemoryFileProvider(byte[] data) : IReaderProvider
{
    public IReader CreateReader(Endianness endian = Endianness.BigEndian) => new MemoryReader(data, endian: endian);
}
