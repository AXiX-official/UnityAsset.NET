using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography;

using UnityAsset.NET.Extensions;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.BundleFile;

public sealed unsafe class UnityCN
{
    private const string Signature = "#$unity3dchina!@";

    private ICryptoTransform Encryptor;
    
    uint value;
    
    public readonly byte[] InfoBytes;
    public readonly byte[] InfoKey;
    
    public readonly byte[] SignatureBytes;
    public readonly byte[] SignatureKey;

    public readonly byte[] Index = new byte[0x10];
    public readonly byte[] Sub = new byte[0x10];
    
    private GCHandle subHandle;
    private GCHandle indexHandle;
    private readonly byte* subPtr;
    private readonly byte* indexPtr;

    private bool isIndexSpecial = true;

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
        
        for (int i = 0; i < Index.Length; i++)
        {
            if (Index[i] != i)
            {
                isIndexSpecial = false;
                break;
            }
        }
        
        subHandle = GCHandle.Alloc(Sub, GCHandleType.Pinned);
        indexHandle = GCHandle.Alloc(Index, GCHandleType.Pinned);

        subPtr = (byte*)subHandle.AddrOfPinnedObject();
        indexPtr = (byte*)indexHandle.AddrOfPinnedObject();
    }
    
    ~UnityCN()
    {
        if (subHandle.IsAllocated) subHandle.Free();
        if (indexHandle.IsAllocated) indexHandle.Free();
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
        
        subHandle = GCHandle.Alloc(Sub, GCHandleType.Pinned);
        indexHandle = GCHandle.Alloc(Index, GCHandleType.Pinned);

        subPtr = (byte*)subHandle.AddrOfPinnedObject();
        indexPtr = (byte*)indexHandle.AddrOfPinnedObject();
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
    
    public void DecryptAndDecompress(ReadOnlySpan<byte> compressedData, Span<byte> decompressedData, int index)
    {
        fixed (byte* sourcePtr = &compressedData.GetPinnableReference())
        fixed (byte* targetPtr = &decompressedData.GetPinnableReference())
        {
            byte* s = sourcePtr;
            byte* t = targetPtr;
            byte* sourceEnd = sourcePtr + compressedData.Length;
            byte* targetEnd = targetPtr + decompressedData.Length;
            while (s < sourceEnd)
            {
                var innerIndex = index;
                var token = DecryptByteSp(*s++, innerIndex++);
                var literalLength = token >> 4;
                var matchLength = token & 0xF;
                if (literalLength != 0xF)
                {
                    if (s + 0xF <= sourceEnd)
                    {
                        *(Int128*)t = *(Int128*)s;
                    }
                    else
                    {
                        Buffer.MemoryCopy(s, t, literalLength, literalLength);
                    }
                }
                else
                {
                    int b;
                    do
                    {
                        b = DecryptByteSp(*s++, innerIndex++);
                        literalLength += b;
                    } while (b == 0xFF);
                    Buffer.MemoryCopy(s, t, literalLength, literalLength);
                }
                
                s += literalLength;
                t += literalLength;
                
                if (s == sourceEnd && matchLength == 0) break;
                if (s >= sourceEnd) throw new Exception("Invalid compressed data");
                
                var offset = (int)DecryptByteSp(*s++, innerIndex++);
                offset |= DecryptByteSp(*s++, innerIndex++) << 8;
                if (matchLength == 0xF)
                {
                    int b;
                    do
                    {
                        b = DecryptByteSp(*s++, innerIndex++);
                        matchLength += b;
                    } while (b == 0xFF);
                }

                matchLength += 4;
                
                if (matchLength <= offset)
                {
                    if (matchLength <= 0xF && t + 0xF <= targetEnd)
                    {
                        *(Int128*)t = *(Int128*)(t - offset);
                    }
                    else
                    {
                        Buffer.MemoryCopy(t - offset, t, matchLength, matchLength);
                    }
                }
                else
                {
                    while (matchLength > offset)
                    {
                        Buffer.MemoryCopy(t - offset, t, offset, offset);
                        t += offset;
                        matchLength -= offset;
                    }
                    
                    Buffer.MemoryCopy(t - offset, t, matchLength, matchLength);
                }
                t += matchLength;
                index++;
            }
        }
    }
    

    [Obsolete("This method is obsolete. Use DecryptAndDecompress instead.")]
    public void DecryptBlock(Span<byte> bytes, int size, int index)
    {
        var offset = 0;
        while (offset < size)
        {
            offset += Decrypt(bytes[offset..], index++, size - offset);
        }
    }
    
    public void EncryptBlock(Span<byte> bytes, int size, int index)
    {
        var offset = 0;
        while (offset < size)
        {
            offset += Encrypt(bytes[offset..], index++, size - offset);
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
        bytes[offset] = (byte)((Index[bytes[offset] & 0xF] - b) & 0xF | (Index[bytes[offset] >> 4] - b) << 4) ;
        b = bytes[offset];
        offset++;
        index++;
        return b;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte DecryptByteUnsafe(byte b, int index)
    {
        var mb = subPtr[index & 3]
                 + subPtr[((index >> 2) & 3) + 4]
                 + subPtr[((index >> 4) & 3) + 8]
                 + subPtr[((byte)index >> 6) + 12];
        return (byte)((indexPtr[b & 0xF] - mb) & 0xF | (indexPtr[b >> 4] - mb) << 4);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte DecryptByteSp(byte b, int index)
    {
        var mb = subPtr[index & 3]
                 + subPtr[((index >> 2) & 3) + 4]
                 + subPtr[((index >> 4) & 3) + 8]
                 + subPtr[((byte)index >> 6) + 12];
        if (isIndexSpecial)
        {
            return (byte)(((b & 0xF) - mb) & 0xF | ((b >> 4) - mb) << 4);
        }
        else
        {
            return (byte)((indexPtr[b & 0xF] - mb) & 0xF | (indexPtr[b >> 4] - mb) << 4);
        }
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