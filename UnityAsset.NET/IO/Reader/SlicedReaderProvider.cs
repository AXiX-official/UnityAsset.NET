using UnityAsset.NET.Enums;

namespace UnityAsset.NET.IO.Reader;

public class SlicedReaderProvider : IReaderProvider
{
    protected readonly IReaderProvider BaseReaderProvider;
    protected readonly ulong Start;
    protected readonly ulong Length;
    
    public SlicedReaderProvider(IReaderProvider readerProvider, ulong start, ulong length)
    {
        BaseReaderProvider = readerProvider;
        Start = start;
        Length = length;
    }

    public IReader CreateReader(Endianness endian) => new SlicedReader(BaseReaderProvider, Start, Length, endian);
}