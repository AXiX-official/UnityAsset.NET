namespace UnityAsset.NET.Enums;

[Flags]
public enum StorageBlockFlags
{
    CompressionTypeMask = 0x3f,
    Streamed = 0x40,
}