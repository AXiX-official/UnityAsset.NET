using UnityAsset.NET.Files.BundleFiles;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files;

public static class BundleFileExtension
{
    public static List<Asset> Assets(this BundleFile bf) =>
        bf.Files
            .Where(file => file.File is SerializedFile)
            .SelectMany(file => ((SerializedFile)file.File).Assets)
            .ToList();

    public static void PatchCrc32(this BundleFile bf, uint newCrc32)
    {
        if (bf.Crc32 != newCrc32)
        {
            var patchBytes = CRC32.rCRC(newCrc32, bf.Crc32);
            bf.Files.Add(new FileWrapper(new DataBuffer(patchBytes), new FileEntry(0, 4, 0, "crc32-patch-data")));
        }
    }
}