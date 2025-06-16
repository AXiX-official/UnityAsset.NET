using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.SerializedFiles;

public struct Hash128
{
    public byte[] data; //16 bytes

    public Hash128(byte[] data)
    {
        this.data = data;
    }
    public Hash128(DataBuffer db)
    {
        data = db.ReadBytes(16);
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
        StringBuilder hex = new StringBuilder(data.Length * 2);

        foreach (byte b in data.AsSpan())
        {
            hex.AppendFormat("{0:x2}", b);
        }

        return hex.ToString();
    }

    public static Hash128 NewBlankHash()
    {
        return new Hash128 { data = new byte[16] };
    }
    
    public int Serialize(DataBuffer db)
    {
        return db.WriteBytes(data);
    }
}