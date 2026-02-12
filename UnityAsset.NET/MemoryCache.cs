using Microsoft.Extensions.Caching.Memory;

namespace UnityAsset.NET;

public sealed class CustomMemoryCache<TKey, TValue>
    where TKey : notnull
{
    private MemoryCache _cache;
    
    public CustomMemoryCache(long maxSize)
    {
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = maxSize
        });
    }
    
    public TValue GetOrCreate(
        TKey key,
        Func<TValue> factory,
        long size)
    {
        var lazy = _cache.GetOrCreate(key, entry =>
        {
            entry.SetSize(size);

            
            var lazyValue = new Lazy<TValue>(
                factory,
                LazyThreadSafetyMode.ExecutionAndPublication);

            /*entry.RegisterPostEvictionCallback((k, v, reason, state) =>
            {
                
            });*/

            return lazyValue;
        });

        try
        {
            return lazy!.Value;
        }
        catch
        {
            Remove(key);
            throw;
        }
    }

    public void Remove(TKey key)
    {
        _cache.Remove(key);
    }
    
    public void Reset(long maxSize)
    {
        _cache.Dispose();
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = maxSize
        });
    }
}
