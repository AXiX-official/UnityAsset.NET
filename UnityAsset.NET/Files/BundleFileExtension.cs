using UnityAsset.NET.Files.SerializedFiles;

namespace UnityAsset.NET.Files;

public static class BundleFileExtension
{
    public static List<Asset> Assets(this BundleFile bf) =>
        bf.Files
            .Where(file => file.File is SerializedFile)
            .SelectMany(file => ((SerializedFile)file.File).Assets)
            .ToList();
}