using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using static UnityAsset.NET.Enums.SerializedFileFormatVersion;

namespace UnityAsset.NET.Files.SerializedFiles;

public sealed class SerializedType
{
    public Int32 TypeID;
    public bool IsStrippedType;
    public Int16 ScriptTypeIndex;
    public Hash128? ScriptIdHash;
    public Hash128 TypeHash;
    public bool IsRefType;
    public TypeTreeNode[] Nodes;
    public byte[]? StringBufferBytes;
    public int[]? TypeDependencies;
    public SerializedTypeReference? TypeReference;
    public bool IsNamed;
    public Hash128[] SubTreeHashList;

    public SerializedType(Int32 typeId, bool isStrippedType, Int16 scriptTypeIndex, Hash128? scriptIdHash, Hash128 typeHash, bool isRefType, TypeTreeNode[] nodes, byte[]? stringBufferBytes, int[]? typeDependencies, SerializedTypeReference? typeReference, Hash128[] subTreeHashList)
    {
        TypeID = typeId;
        IsStrippedType = isStrippedType;
        ScriptTypeIndex = scriptTypeIndex;
        ScriptIdHash = scriptIdHash;
        TypeHash = typeHash;
        IsRefType = isRefType;

        var entry = TypeTreeIntern.GetOrAdd(typeHash, nodes, typeId);
        Nodes = entry.Nodes;
        IsNamed = entry.Repr?.IsNamed ?? false;

        StringBufferBytes = stringBufferBytes;
        TypeDependencies = typeDependencies;
        TypeReference = typeReference;
        
        SubTreeHashList = subTreeHashList;
    }

    public static SerializedType Parse(IReader reader, SerializedFileFormatVersion version, bool typeTreeEnabled, bool isRefType, Dictionary<Hash128, List<SerializedType>> extractedTypeTrees)
    {
        var typeId = reader.ReadInt32();
        var isStrippedType = reader.ReadBoolean();
        var scriptTypeIndex = reader.ReadInt16();
        Hash128? scriptIdHash = null;
        if ((version >= RefactorTypeData && typeId == (int)AssetClassID.MonoBehaviour) ||
            (isRefType && scriptTypeIndex >= 0))
        {
            scriptIdHash = new Hash128(reader);
        }
        var typeHash = new Hash128(reader);
        TypeTreeNode[] nodes = [];
        byte[]? stringBufferBytes = null;
        int[]? typeDependencies = null;
        SerializedTypeReference? typeReference = null;
        Hash128? typeTreeHash = null;
        bool IsTypeTreeExtracted = false;
        Hash128[] subTreeHashList = [];
        if (typeTreeEnabled)
        {
            if (version >= ExtractedTypeTreeSupport)
            {
                typeTreeHash = new Hash128(reader);
                var typeTreeSize = reader.ReadInt32();
                if (typeTreeSize != 0)
                {
                    ReadTypeTreeBlob(reader, version, out nodes, out stringBufferBytes);
                }

                if (nodes.Length == 0) IsTypeTreeExtracted = true;
            }
            else
            {
                ReadTypeTreeBlob(reader, version, out nodes, out stringBufferBytes);
            }

            if (version >= SharedSubTreeSupport && IsTypeTreeExtracted)
            {
                subTreeHashList = reader.ReadArray(r => new Hash128(r));
            }

            if (version >= StoresTypeDependencies)
            {
                if (isRefType)
                    typeReference = SerializedTypeReference.Parse(reader);
                else
                    typeDependencies = reader.ReadArray(r => r.ReadInt32());
            }
        }
        var ret = new SerializedType(typeId, isStrippedType, scriptTypeIndex, scriptIdHash, typeHash, isRefType, nodes, stringBufferBytes, typeDependencies, typeReference, subTreeHashList);

        if (IsTypeTreeExtracted)
        {
            if (extractedTypeTrees.TryGetValue(typeTreeHash!.Value, out var types))
            {
                types.Add(ret);
            }
            else
            {
                extractedTypeTrees[typeTreeHash.Value] = [ ret ];
            }
        }
        
        return ret;
    }
    
    internal static void ReadTypeTreeBlob(IReader reader, SerializedFileFormatVersion version, out TypeTreeNode[] nodes, out byte[]? stringBufferBytes)
    {
        if (version >= ExtractedTypeTreeSupport)
            ValidTypeTreeHeader(reader, version);
        int typeTreeNodeCount = reader.ReadInt32();
        int stringBufferLen = reader.ReadInt32();
        nodes = reader.ReadArray(typeTreeNodeCount, r => TypeTreeNode.Parse(r, version));
        stringBufferBytes = reader.ReadBytes(stringBufferLen);
        MemoryReader sr = new MemoryReader(stringBufferBytes);
        for (int i = 0; i < typeTreeNodeCount; i++)
        {
            var node = nodes[i];
            node.Name = ReadString(sr, node.NameStringOffset);
            node.Type = ReadString(sr, node.TypeStringOffset);
        }

        if (nodes[0].Level != 0)
        {
            throw new Exception(
                $"The first node of TypeTreeNodes should have a level of 0 but gets {nodes[0].Level}");
        }
    }

    private static void ValidTypeTreeHeader(IReader reader, SerializedFileFormatVersion version)
    {
        var typeTreeSign = reader.ReadInt32(); //0x7474686d ("tthm")
        if (typeTreeSign != 0x7474686d)
            throw new Exception($"Invalid TypeTree signature: {typeTreeSign:X8}");
        var typeTreeVersion = reader.ReadInt32();
        if (version < IndependentTypeTreeVersion) //6000.5.0a5 - 6000.7.0a6
        {
            var serializedFileVersion = (SerializedFileFormatVersion)typeTreeVersion;
            if (serializedFileVersion != version)
            {
                throw new Exception($"TypeTree version {serializedFileVersion} does not match SerializedFile version {version}");
            }
        }
    }

    private static string ReadString(IReader reader, uint value)
    {
        if ((value & 0x80000000) == 0)
        {
            reader.Seek((int)value);
            return reader.ReadNullTerminatedString();
        }
        var offset = value & 0x7FFFFFFF;
        if (CommonString.StringBuffer.TryGetValue(offset, out var str))
            return str;
        return offset.ToString();
    }

    public void Serialize(IWriter writer, SerializedFileFormatVersion version, bool typeTreeEnabled, bool isRefType)
    {
        writer.WriteInt32(TypeID);
        writer.WriteBoolean(IsStrippedType);
        writer.WriteInt16(ScriptTypeIndex);
        if ((version >= RefactorTypeData && TypeID == (int)AssetClassID.MonoBehaviour) ||
            (isRefType && ScriptTypeIndex > 0))
            ScriptIdHash?.Serialize(writer);
        TypeHash.Serialize(writer);
        if (typeTreeEnabled)
        {
            if (Nodes == null || StringBufferBytes == null)
                throw new NullReferenceException("Nodes or StringBufferBytes is null");
            writer.WriteInt32(Nodes.Length);
            writer.WriteInt32(StringBufferBytes.Length);
            writer.WriteArray(Nodes, (w, node) => node.Serialize(w));
            writer.WriteBytes(StringBufferBytes);
            if (version >= StoresTypeDependencies)
            {
                if (!isRefType && TypeDependencies != null)
                    writer.WriteArrayWithCount(TypeDependencies, (w, i) => w.WriteInt32(i));
                else if(TypeReference != null)
                    TypeReference.Serialize(writer);
                else
                    throw new NullReferenceException($"{(isRefType ? "TypeReference" : "TypeDependencies")} is null");
            }
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Serialized Type:");
        sb.AppendFormat("TypeID: {0} | ", TypeID);
        sb.AppendFormat("IsStrippedType: {0} | ", IsStrippedType);
        sb.AppendFormat("ScriptTypeIndex: {0} | ", ScriptTypeIndex);
        sb.AppendFormat("ScriptIdHash: {0} | ", ScriptIdHash?.ToString() ?? "null");
        sb.AppendFormat("TypeHash: {0} | ", TypeHash);
        sb.AppendFormat("IsRefType: {0} | ", IsRefType);
        sb.AppendFormat("Nodes: {0} | ", Nodes.Length);
        sb.AppendFormat("StringBufferBytes: {0} | ", StringBufferBytes?.Length ?? 0);
        sb.AppendFormat("TypeDependencies: {0} | ", TypeDependencies?.Length ?? 0);
        sb.AppendFormat("TypeReference: {0} | ", TypeReference);
        sb.AppendLine("Nodes:");
        foreach (var node in Nodes.AsSpan())
            sb.AppendLine(node.ToString());
        sb.AppendLine();
        return sb.ToString();
    }

    public string ToTypeName()
    {
        if (Nodes.Length == 0)
            return ((AssetClassID)TypeID).ToString();
        return Nodes[0].Type;
    }

    public string Describe()
    {
        ArgumentNullException.ThrowIfNull(this);

        var name = ToTypeName();
        var className = string.IsNullOrEmpty(name) ? "(unnamed)" : name;
        var hash = TypeHash.data is { Length: > 0 } data
            ? Convert.ToHexString(data)
            : "(none)";

        return $"{className} (class id {TypeID}, hash {hash})";
    }
}