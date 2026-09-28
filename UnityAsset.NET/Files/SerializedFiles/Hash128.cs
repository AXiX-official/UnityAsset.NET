using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.SerializedFiles;

public struct Hash128 : IEquatable<Hash128>
{
    public readonly byte[] data; //16 bytes

    public Hash128(byte[] data)
    {
        this.data = data;
    }
    public Hash128(IReader reader)
    {
        data = reader.ReadBytes(16);
    }

    public bool IsZero()
    {
        if (data == null)
            return true;
        foreach (var b in data.AsSpan())
            if (b != 0)
                return false;
        return true;
    }

    public override string ToString()
    {
        if (data is null)
            return string.Empty;

        StringBuilder hex = new StringBuilder(data.Length * 2);

        foreach (byte b in data.AsSpan())
        {
            hex.AppendFormat("{0:x2}", b);
        }

        return hex.ToString();
    }

    public static Hash128 NewBlankHash()
    {
        return new Hash128(new byte[16]);
    }

    public bool Equals(Hash128 other)
    {
        if (ReferenceEquals(data, other.data)) return true;
        if (data is null || other.data is null) return false;
        return data.AsSpan().SequenceEqual(other.data.AsSpan());
    }

    public override bool Equals(object? obj)
    {
        return obj is Hash128 other && Equals(other);
    }

    public override int GetHashCode()
    {
        if (data is null || data.Length == 0)
        {
            return 0;
        }

        unchecked
        {
            var hash = 17;
            foreach (var b in data)
                hash = hash * 31 + b;

            return hash * 31 + data.Length;
        }
    }

    public void Serialize(IWriter writer) => writer.WriteBytes(data);
}