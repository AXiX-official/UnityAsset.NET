using System.Runtime.InteropServices;

namespace UnityAsset.NET.Extensions;

public static class ListExtensions
{
    public static Span<T> AsSpan<T>(this List<T> list)
    {
        return CollectionsMarshal.AsSpan(list);
    }
    
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this List<T> list)
    {
        return list.AsSpan();
    }
}