using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace UnityAsset.NET.IO.Reader
{
    public sealed class BlockCacheContext
    {
        public static BlockCacheContext Shared { get; } = new(Setting.DefaultBlockCacheSize);

        public BlockCacheContext(long budgetBytes)
        {
            BudgetBytes = budgetBytes;
            Cache = new BlockCache(budgetBytes);
        }

        public BlockCache Cache { get; private set; }

        public ConcurrentDictionary<BlockCacheKey, (int parsed, int total, long size)> AssetToBlockCache { get; } = new();

        public long BudgetBytes { get; private set; }

        public long TotalBlockSize => AssetToBlockCache.Values.Select(value => value.size).Sum();

        public MemoryCacheStatistics? Statistics => Cache.GetCurrentStatistics();

        public void Reset(long budgetBytes)
        {
            BudgetBytes = budgetBytes;
            Cache.Reset(budgetBytes);
        }

        public void ClearAccounting() => AssetToBlockCache.Clear();

        public void RemoveBlocksReferencedByAtMost(int count = 2)
        {
            var keysToRemove = AssetToBlockCache
                .Where(entry => entry.Value.total <= count)
                .Select(entry => entry.Key)
                .ToList();

            foreach (var key in keysToRemove)
                AssetToBlockCache.TryRemove(key, out _);
        }
    }
}