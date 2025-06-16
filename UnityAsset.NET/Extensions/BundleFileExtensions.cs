using System.Runtime.InteropServices;
using UnityAsset.NET.Files;
using UnityAsset.NET.Files.BundleFiles;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Extensions;

public static class BundleFileExtensions
{
    public static void ParseFilesWithTypeConversion(this BundleFile bf)
    {
        if (bf.Files == null) throw new NullReferenceException();
        var filesSpan = bf.Files.AsSpan();
        for (int i = 0; i < filesSpan.Length; i++)
        {
            ref var file = ref filesSpan[i];
            if (file is { File: DataBuffer db, CanBeSerializedFile: true })
                    file = new FileWrapper(SerializedFile.Parse(db), file.Info);
        }
    }
    
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