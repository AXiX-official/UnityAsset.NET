using UnityAsset.NET.Enums;
using UnityAsset.NET.Files.BundleFiles;

namespace UnityAsset.NET.IO;

public class BlockStream : Stream
{
    private class BlockInfo
    {
        public UInt32 UncompressedSize;
        public UInt32 CompressedSize;
        public long Start;
        public long End => Start + CompressedSize;
        public CompressionType CompressionType;

        public BlockInfo(StorageBlockInfo info, long offset)
        {
            UncompressedSize = info.UncompressedSize;
            CompressedSize = info.CompressedSize;
            Start = offset;
            CompressionType = (CompressionType)(info.Flags & StorageBlockFlags.CompressionTypeMask);
        }
    }
    
    private readonly List<BlockInfo> _blocks;
    private readonly StreamReader _baseReader;
    private MemoryStream _currentBlockData;
    private int _currentBlockIndex = -1;
    private long _positionInUncompressedData;
    private readonly long _length;
    private readonly long _offset;
    private readonly UnityCN? _unityCnInfo;
    
    public BlockStream(List<StorageBlockInfo> blocks, StreamReader baseReader, UnityCN? unityCnInfo = null)
    {
        _blocks = new List<BlockInfo>();
        long offset = 0;
        foreach (var block in blocks)
        {
            _blocks.Add(new BlockInfo(block, offset));
            offset += block.CompressedSize;
        }
        _baseReader = baseReader;
        _offset = baseReader.Position;
        _length = blocks.Sum(b => (long)b.UncompressedSize);
        _unityCnInfo = unityCnInfo;
    }
    
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;
    
    public override long Position
    {
        get => _positionInUncompressedData;
        set => Seek(value, SeekOrigin.Begin);
    }
    
    private void EnsureBlockLoaded(long position)
    {
        for (int i = 0; i < _blocks.Count; i++)
        {
            var block = _blocks[i];
            if (position >= block.Start && position < block.End)
            {
                if (i != _currentBlockIndex)
                {
                    _baseReader.Seek(block.Start + _offset);
                    var compressedData = _baseReader.ReadBytes((int)block.CompressedSize);
                    var uncompressedData = new byte[block.UncompressedSize];
                    if (_unityCnInfo == null)
                        Compression.DecompressToBytes(compressedData, uncompressedData, block.CompressionType);
                    else
                        _unityCnInfo.DecryptAndDecompress(compressedData, uncompressedData, i);
                    _currentBlockData = new MemoryStream(uncompressedData);
                    _currentBlockIndex = i;
                }
                _currentBlockData.Position = position - block.Start;
                return;
            }
        }
        
        throw new ArgumentOutOfRangeException(nameof(position), "Position is beyond the end of the data");
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        
        while (count > 0)
        {
            EnsureBlockLoaded(_positionInUncompressedData);
            
            long remainingInBlock = _currentBlockData.Length - _currentBlockData.Position;
            int bytesToRead = (int)Math.Min(remainingInBlock, count);
            
            if (bytesToRead == 0)
                break;
                
            int bytesRead = _currentBlockData.Read(buffer, offset, bytesToRead);
            totalRead += bytesRead;
            offset += bytesRead;
            count -= bytesRead;
            _positionInUncompressedData += bytesRead;
            
            if (bytesRead < bytesToRead)
                break;
        }
        
        return totalRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _positionInUncompressedData + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentException("Invalid seek origin")
        };
        
        if (newPosition < 0 || newPosition > Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        
        _positionInUncompressedData = newPosition;
        return newPosition;
    }

    // Other required Stream methods (can throw NotSupportedException for write operations)
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}