using UnityAsset.NET.Enums;

namespace UnityAsset.NET.IO;

public class FileStreamReader : StreamReader
{
    public FileStreamReader(string filePath, Endianness endian = Endianness.BigEndian, FileShare fileShare = FileShare.Read)
        : base(new FileStream(filePath, FileMode.Open, FileAccess.Read, fileShare), endian, false)
    {
    }
}