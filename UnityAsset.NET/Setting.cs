namespace UnityAsset.NET;

public static class Setting
{
    public static Int32 DefaultChunkSize = 0x00020000;
    public static string? DefaultUnityCNKey = null;
    public static string DefaultUnityVerion = "2019.4.40f1";
    public static string DefaultTpkFilePath = "./uncompressed.tpk";
    public static long DefaultBlockCacheSize = 2L * 1024 * 1024 * 1024;
}