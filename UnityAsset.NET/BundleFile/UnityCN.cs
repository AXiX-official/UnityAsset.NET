using System.Text;
using System.Security.Cryptography;

using UnityAsset.NET.Extensions;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.BundleFile;

public sealed class UnityCN
{
    private const string Signature = "#$unity3dchina!@";

    private ICryptoTransform Encryptor;
    
    uint value;
    
    public byte[] InfoBytes = new byte[0x10];
    public byte[] InfoKey = new byte[0x10];
    
    public byte[] SignatureBytes = new byte[0x10];
    public byte[] SignatureKey = new byte[0x10];

    public byte[] Index = new byte[0x10];
    public byte[] Sub = new byte[0x10];

    public UnityCN(AssetReader reader, string key)
    {
        SetKey(key);
        
        value = reader.ReadUInt32();

        InfoBytes = reader.ReadBytes(0x10);
        InfoKey = reader.ReadBytes(0x10);
        reader.Position += 1;

        SignatureBytes = reader.ReadBytes(0x10);
        SignatureKey = reader.ReadBytes(0x10);
        reader.Position += 1;
        
        reset();
    }

    public UnityCN(string key)
    {
        SetKey(key);

        value = 0;
        
        InfoBytes = [0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF, 0xA6, 0xB1, 0xDE, 0x48, 0x9E, 0x2B, 0x53, 0x5C];
        InfoKey = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10];
        EncryptKey(InfoKey, InfoBytes);
        
        SignatureKey = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10];
        SignatureBytes = Encoding.UTF8.GetBytes(Signature);
        EncryptKey(SignatureKey, SignatureBytes);
        
        reset();
    }

    public void reset()
    {
        var infoBytes = (byte[])InfoBytes.Clone();
        var infoKey = (byte[])InfoKey.Clone();
        var signatureBytes = (byte[])SignatureBytes.Clone();
        var signatureKey = (byte[])SignatureKey.Clone();
        
        DecryptKey(signatureKey, signatureBytes);

        var str = Encoding.UTF8.GetString(signatureBytes);
        if (str != Signature)
        {
            throw new Exception($"Invalid Signature, Expected {Signature} but found {str} instead");
        }
        
        DecryptKey(infoKey, infoBytes);

        infoBytes = infoBytes.ToUInt4Array();
        infoBytes.AsSpan(0, 0x10).CopyTo(Index);
        var subBytes = infoBytes.AsSpan(0x10, 0x10);
        for (var i = 0; i < subBytes.Length; i++)
        {
            var idx = (i % 4 * 4) + (i / 4);
            Sub[idx] = subBytes[i];
        }
    }
    
    public void Write(AssetWriter writer)
    {
        writer.WriteUInt32(value);
        writer.Write(InfoBytes);
        writer.Write(InfoKey);
        writer.Write((byte)0);
        writer.Write(SignatureBytes);
        writer.Write(SignatureKey);
        writer.Write((byte)0);
    }

    public bool SetKey(string key)
    {
        try
        {
            using var aes = Aes.Create();
            aes.Mode = CipherMode.ECB;
            aes.Key = Convert.FromHexString(key);

            Encryptor = aes.CreateEncryptor();
        }
        catch (Exception e)
        {
            Console.WriteLine($"[UnityCN] Invalid key !!\n{e.Message}");
            return false;
        }
        return true;
    }

    public void DecryptBlock(Span<byte> bytes, int size, int index)
    {
        var offset = 0;
        while (offset < size)
        {
            offset += Decrypt(bytes.Slice(offset), index++, size - offset);
        }
    }
    
    public void EncryptBlock(Span<byte> bytes, int size, int index)
    {
        var offset = 0;
        while (offset < size)
        {
            offset += Encrypt(bytes.Slice(offset), index++, size - offset);
        }
    }

    private void DecryptKey(byte[] key, byte[] data)
    {
        if (Encryptor != null)
        {
            key = Encryptor.TransformFinalBlock(key, 0, key.Length);
            for (int i = 0; i < 0x10; i++)
                data[i] ^= key[i];
        }
    }
    
    private void EncryptKey(byte[] key, byte[] data)
    {
        if (Encryptor != null)
        {
            key = Encryptor.TransformFinalBlock(key, 0, key.Length);
            for (int i = 0; i < 0x10; i++)
                data[i] ^= key[i];
        }
    }

    private int DecryptByte(Span<byte> bytes, ref int offset, ref int index)
    {
        var b = Sub[((index >> 2) & 3) + 4] + Sub[index & 3] + Sub[((index >> 4) & 3) + 8] + Sub[((byte)index >> 6) + 12];
        bytes[offset] = (byte)((Index[bytes[offset] & 0xF] - b) & 0xF | 0x10 * (Index[bytes[offset] >> 4] - b));
        b = bytes[offset];
        offset++;
        index++;
        return b;
    }
    
    private int EncryptByte(Span<byte> bytes, ref int offset, ref int index)
    {
        byte currentByte = bytes[offset];
        var low = currentByte & 0xF;
        var high = currentByte >> 4;
        
        var b = Sub[((index >> 2) & 3) + 4] + Sub[index & 3] + Sub[((index >> 4) & 3) + 8] + Sub[((byte)index >> 6) + 12];
        
        int i = 0;
        while (((Index[i] - b) & 0xF) != low && i < 0x10)
        {
            i++;
        }
        low = i;
        i = 0;
        while (((Index[i] - b) & 0xF) != high && i < 0x10)
        {
            i++;
        }
        high = i;

        bytes[offset] = (byte)(low | (high << 4));
        offset++;
        index++;
        return currentByte;
    }

    private int Decrypt(Span<byte> bytes, int index, int remaining)
    {
        var offset = 0;

        var curByte = DecryptByte(bytes, ref offset, ref index);
        var byteHigh = curByte >> 4;
        var byteLow = curByte & 0xF;

        if (byteHigh == 0xF)
        {
            int b;
            do
            {
                b = DecryptByte(bytes, ref offset, ref index);
                byteHigh += b;
            } while (b == 0xFF);
        }

        offset += byteHigh;

        if (offset < remaining)
        {
            DecryptByte(bytes, ref offset, ref index);
            DecryptByte(bytes, ref offset, ref index);
            if (byteLow == 0xF)
            {
                int b;
                do
                {
                    b = DecryptByte(bytes, ref offset, ref index);
                } while (b == 0xFF);
            }
        }

        return offset;
    }
    
    private int Encrypt(Span<byte> bytes, int index, int remaining)
    {
        var offset = 0;
        
        var curByte = EncryptByte(bytes, ref offset, ref index);
        var byteHigh = curByte >> 4;
        var byteLow = curByte & 0xF;

        if (byteHigh == 0xF)
        {
            int b;
            do
            {
                b = EncryptByte(bytes, ref offset, ref index);
                byteHigh += b;
            } while (b == 0xFF);
        }

        offset += byteHigh;

        if (offset < remaining)
        {
            EncryptByte(bytes, ref offset, ref index);
            EncryptByte(bytes, ref offset, ref index);
            if (byteLow == 0xF)
            {
                int b;
                do
                {
                    b = EncryptByte(bytes, ref offset, ref index);
                } while (b == 0xFF);
            }
        }

        return offset;
    }
}