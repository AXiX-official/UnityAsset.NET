namespace UnityAsset.NET.Enums;

[Flags]
public enum StorageBlockFlags : UInt16
{
    CompressionTypeMask = 0x3f,
    Streamed = 0x40,
}