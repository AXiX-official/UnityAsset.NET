namespace UnityAsset.NET
{
    public static class Setting
    {
        /// <summary>Chunk size used when writing compressed bundles.</summary>
        public static Int32 DefaultChunkSize = 0x00020000;

        /// <summary>UnityCN key used when a bundle is opened without one; see <see cref="UnitySessionOptions.UnityCnKey"/>.</summary>
        public static string? DefaultUnityCNKey = null;

        /// <summary>Revision assumed for a file that declares none; see <see cref="UnitySessionOptions.DefaultUnityVersion"/>.</summary>
        public static string DefaultUnityVerion = "2019.4.40f1";

        /// <summary>Type tree database used for stripped files; see <see cref="UnitySessionOptions.TpkFilePath"/>.</summary>
        public static string DefaultTpkFilePath = "./uncompressed.tpk";

        /// <summary>Budget for the decompressed block cache.</summary>
        public static long DefaultBlockCacheSize = 2L * 1024 * 1024 * 1024;
    }
}
