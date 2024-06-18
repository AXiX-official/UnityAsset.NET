using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.BundleFile;

public sealed class Header
    {
        public string signature;
        public uint version;
        public string unityVersion;
        public string unityRevision;
        public long size;
        public uint compressedBlocksInfoSize;
        public uint uncompressedBlocksInfoSize;
        public ArchiveFlags flags;

        public Header(AssetReader reader)
        {
            signature = reader.ReadStringToNull(20);
            switch (signature)
            {
                case "UnityFS":
                    version = reader.ReadUInt32();
                    unityVersion = reader.ReadStringToNull();
                    unityRevision = reader.ReadStringToNull();
                    size = reader.ReadInt64();
                    compressedBlocksInfoSize = reader.ReadUInt32();
                    uncompressedBlocksInfoSize = reader.ReadUInt32();
                    flags = (ArchiveFlags)reader.ReadUInt32();
                    break;
                default:
                    throw new Exception("Invalid signature");
            }
        }
        
        public void Write(AssetWriter writer)
        {
            writer.WriteStringToNull(signature);
            writer.WriteUInt32(version);
            writer.WriteStringToNull(unityVersion);
            writer.WriteStringToNull(unityRevision);
            writer.WriteInt64(size);
            writer.WriteUInt32(compressedBlocksInfoSize);
            writer.WriteUInt32(uncompressedBlocksInfoSize);
            writer.WriteUInt32((uint)flags);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("signature: {0} | ", signature);
            sb.AppendFormat("version: {0} | ", version);
            sb.AppendFormat("unityVersion: {0} | ", unityVersion);
            sb.AppendFormat("unityRevision: {0} | ", unityRevision);
            sb.AppendFormat("size: 0x{0:X8} | ", size);
            sb.AppendFormat("compressedBlocksInfoSize: 0x{0:X8} | ", compressedBlocksInfoSize);
            sb.AppendFormat("uncompressedBlocksInfoSize: 0x{0:X8} | ", uncompressedBlocksInfoSize);
            sb.AppendFormat("flags: 0x{0:X8}", (int)flags);
            return sb.ToString();
        }
        
        public long CalculateSize()
        {
            return signature.Length + 1 + 4 + unityVersion.Length + 1 + unityRevision.Length + 1 + 8 + 4 + 4 + 4;
        }
    }