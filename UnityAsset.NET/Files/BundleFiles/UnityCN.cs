using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.BundleFiles;

public sealed unsafe class UnityCN
{
    private const string Signature = "#$unity3dchina!@";

    private ICryptoTransform _encryptor;
    
    public UInt32 Value;
    public readonly byte[] InfoBytes;
    public readonly byte[] InfoKey;
    public readonly byte[] SignatureBytes;
    public readonly byte[] SignatureKey;

    public readonly byte[] Index = new byte[0x10];
    public readonly byte[] Sub = new byte[0x10];
    
    private GCHandle _subHandle;
    private GCHandle _indexHandle;
    private readonly byte* _subPtr;
    private readonly byte* _indexPtr;

    private readonly bool _isIndexSpecial = true;

    public UnityCN(DataBuffer db, string key)
    {
        SetKey(key);
        
        Value = db.ReadUInt32();
        InfoBytes = db.ReadBytes(0x10);
        InfoKey = db.ReadBytes(0x10);
        db.Advance(1);
        SignatureBytes = db.ReadBytes(0x10);
        SignatureKey = db.ReadBytes(0x10);
        db.Advance(1);
        
        Reset();
        
        for (int i = 0; i < Index.Length; i++)
        {
            if (Index[i] != i)
            {
                _isIndexSpecial = false;
                break;
            }
        }
        
        _subHandle = GCHandle.Alloc(Sub, GCHandleType.Pinned);
        _indexHandle = GCHandle.Alloc(Index, GCHandleType.Pinned);

        _subPtr = (byte*)_subHandle.AddrOfPinnedObject();
        _indexPtr = (byte*)_indexHandle.AddrOfPinnedObject();
    }
    
    ~UnityCN()
    {
        if (_subHandle.IsAllocated) _subHandle.Free();
        if (_indexHandle.IsAllocated) _indexHandle.Free();
    }

    public UnityCN(string key)
    {
        SetKey(key);

        Value = 0;
        
        InfoBytes = [0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF, 0xA6, 0xB1, 0xDE, 0x48, 0x9E, 0x2B, 0x53, 0x5C];
        InfoKey = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10];
        XorWithKey(InfoKey, InfoBytes);
        
        SignatureKey = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10];
        SignatureBytes = Encoding.UTF8.GetBytes(Signature);
        XorWithKey(SignatureKey, SignatureBytes);
        
        Reset();
        
        _subHandle = GCHandle.Alloc(Sub, GCHandleType.Pinned);
        _indexHandle = GCHandle.Alloc(Index, GCHandleType.Pinned);

        _subPtr = (byte*)_subHandle.AddrOfPinnedObject();
        _indexPtr = (byte*)_indexHandle.AddrOfPinnedObject();
    }

    public void Reset()
    {
        var infoBytes = (byte[])InfoBytes.Clone();
        var infoKey = (byte[])InfoKey.Clone();
        var signatureBytes = (byte[])SignatureBytes.Clone();
        var signatureKey = (byte[])SignatureKey.Clone();
        
        XorWithKey(signatureKey, signatureBytes);

        var str = Encoding.UTF8.GetString(signatureBytes);
        if (str != Signature)
        {
            throw new Exception($"Invalid Signature, Expected {Signature} but found {str} instead");
        }
        
        XorWithKey(infoKey, infoBytes);
        var buffer = new byte[infoBytes.Length * 2];
        for (var i = 0; i < infoBytes.Length; i++)
        {
            var idx = i * 2;
            buffer[idx] = (byte)(infoBytes[i] >> 4);
            buffer[idx + 1] = (byte)(infoBytes[i] & 0xF);
        }
        buffer.AsSpan(0, 0x10).CopyTo(Index);
        var subBytes = buffer.AsSpan(0x10, 0x10);
        for (var i = 0; i < subBytes.Length; i++)
        {
            var idx = (i % 4 * 4) + (i / 4);
            Sub[idx] = subBytes[i];
        }
    }
    
    public int Serialize(DataBuffer db)
    {
        db.WriteUInt32(Value);
        db.WriteBytes(InfoBytes);
        db.WriteBytes(InfoKey);
        db.WriteByte(0);
        db.WriteBytes(SignatureBytes);
        db.WriteBytes(SignatureKey);
        db.WriteByte(0);
        return 70;
    }

    public long SerializeSize => 70;

    public void SetKey(string key)
    {
        if (key.Length != 32 && key.Length != 16)
            throw new ArgumentException("key must be 32 or 16 characters long");
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Key = Convert.FromHexString(key);

        _encryptor = aes.CreateEncryptor();
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
                var token = DecryptByteUnsafe(*s++, innerIndex++);
                var literalLength = token >> 4;
                var matchLength = token & 0xF;
                if (literalLength != 0xF)
                {
                    if (t + 0x10 <= targetEnd)
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
                        b = DecryptByteUnsafe(*s++, innerIndex++);
                        literalLength += b;
                    } while (b == 0xFF);
                    Buffer.MemoryCopy(s, t, literalLength, literalLength);
                }
                
                s += literalLength;
                t += literalLength;
                
                if (s == sourceEnd && matchLength == 0) break;
                if (s >= sourceEnd) throw new Exception("Invalid compressed data");
                
                var offset = (int)DecryptByteUnsafe(*s++, innerIndex++);
                offset |= DecryptByteUnsafe(*s++, innerIndex++) << 8;
                if (matchLength == 0xF)
                {
                    int b;
                    do
                    {
                        b = DecryptByteUnsafe(*s++, innerIndex++);
                        matchLength += b;
                    } while (b == 0xFF);
                }

                matchLength += 4;
                
                if (matchLength <= offset)
                {
                    if (matchLength <= 0x10 && t + 0x10 <= targetEnd)
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
    
    public void EncryptBlock(Span<byte> bytes, int size, int index)
    {
        var offset = 0;
        while (offset < size)
        {
            offset += Encrypt(bytes[offset..], index++, size - offset);
        }
    }
    
    private void XorWithKey(byte[] key, byte[] data)
    {
        key = _encryptor.TransformFinalBlock(key, 0, key.Length);
        for (int i = 0; i < 0x10; i++)
            data[i] ^= key[i];
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte DecryptByteUnsafe(byte b, int index)
    {
        var mb = _subPtr[index & 3]
                 + _subPtr[((index >> 2) & 3) + 4]
                 + _subPtr[((index >> 4) & 3) + 8]
                 + _subPtr[((byte)index >> 6) + 12];
        if (_isIndexSpecial)
        {
            return (byte)(((b & 0xF) - mb) & 0xF | ((b >> 4) - mb) << 4);
        }
        else
        {
            return (byte)((_indexPtr[b & 0xF] - mb) & 0xF | (_indexPtr[b >> 4] - mb) << 4);
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
            i++;
        low = i;
        i = 0;
        while (((Index[i] - b) & 0xF) != high && i < 0x10)
            i++;
        high = i;

        bytes[offset] = (byte)(low | (high << 4));
        offset++;
        index++;
        return currentByte;
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