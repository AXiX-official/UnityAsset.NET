using UnityAsset.NET.Files;
using UnityAsset.NET.Files.BundleFiles;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;

namespace UnityAsset.NET.Extensions;

public static class BundleFileExtensions
{
    public static List<Asset> Assets(this BundleFile bf) =>
        bf.Files
            .Where(file => file.File is SerializedFile)
            .SelectMany(file => ((SerializedFile)file.File).Assets)
            .ToList();

    public static uint CalculateCrc32(this BundleFile bf)
    {
        if (bf.Files.Any(file => file.File is not IReaderProvider))
            throw new ArgumentException("The bundle file must contain at least one IReaderProvider.");

        var buffer = new byte[8092];
        uint crc = 0;
        foreach (var file in bf.Files)
        {
            var rp = (IReaderProvider)file.File;
            var reader = rp.CreateReader();
            while (reader.Remaining > 0)
            {
                var bytesRead = reader.Read(buffer, 0, 8092);
                crc = CRC32.CalculateCRC32(buffer.AsSpan(0, bytesRead), crc);
            }
        }

        return crc;
    }
    
    public static void PatchCrc32(this BundleFile bf, uint newCrc32)
    {
        var oldCrc = bf.CalculateCrc32();
        if (newCrc32 == oldCrc)
            return;
        var patch = CRC32.rCRC(oldCrc, newCrc32);
        var offset = bf.Files[^1].Info.Offset + bf.Files[^1].Info.Size;
        var entry = new FileEntry(offset, (ulong)patch.Length, 0, "crc32Patch");
        bf.Files.Add(new (new MemoryReaderProvider(patch), entry));
    }
}