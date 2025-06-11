namespace UnityAsset.NET.Enums;

[Flags]
public enum ArchiveFlags : UInt32
{
    CompressionTypeMask = 0x3f,
    BlocksAndDirectoryInfoCombined = 0x40,
    BlocksInfoAtTheEnd = 0x80,
    OldWebPluginCompatibility = 0x100,
    BlockInfoNeedPaddingAtStart = 0x200,
    UnityCNEncryption = 0x400,
    UnityCNEncryptionNew = 0x1000
}