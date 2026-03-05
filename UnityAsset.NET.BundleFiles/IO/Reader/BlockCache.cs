using System.Buffers;
using Microsoft.Extensions.Caching.Memory;
using UnityAsset.NET.FileSystem;

namespace UnityAsset.NET.IO.Reader
{
    #if NETSTANDARD2_1
    public struct BlockCacheKey : IEquatable<BlockCacheKey>
    {
        public readonly IVirtualFile File;
        public readonly int BlockIndex;
        
        public BlockCacheKey(IVirtualFile file, int blockIndex)
        {
            File = file;
            BlockIndex = blockIndex;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(File, BlockIndex);
        }
        
        public bool Equals(BlockCacheKey other)
        {
            return ReferenceEquals(File, other.File)
                   && BlockIndex == other.BlockIndex;
        }

        public override bool Equals(object? obj)
            => obj is BlockCacheKey other && Equals(other);
    }
    #else
    public readonly record struct BlockCacheKey(IVirtualFile File, int BlockIndex);
    #endif

    public sealed class BlockCache
    {
        private MemoryCache _cache;

        public BlockCache(long maxSize)
        {
            _cache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = maxSize,
                TrackStatistics = true
            });
        }

        public byte[] GetOrCreate(
            BlockCacheKey key,
            Func<byte[]> factory,
            long size)
        {
            var lazy = _cache.GetOrCreate(key, entry =>
            {
                entry.SetSize(size);

                var lazyValue = new Lazy<byte[]>(
                    factory,
                    LazyThreadSafetyMode.ExecutionAndPublication);

                entry.RegisterPostEvictionCallback((k, v, reason, state) =>
                {
                    ArrayPool<byte>.Shared.Return(((Lazy<byte[]>)v!).Value);
                });

                return lazyValue;
            });

            try
            {
                return lazy!.Value;
            }
            catch
            {
                _cache.Remove(key);
                throw;
            }
        }

        public void Remove(BlockCacheKey key)
        {
            _cache.Remove(key);
        }

        public void Reset(long maxSize)
        {
            _cache.Dispose();
            _cache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = maxSize,
                TrackStatistics = true
            });
        }

        public MemoryCacheStatistics? GetCurrentStatistics() => _cache.GetCurrentStatistics();
    }
}
