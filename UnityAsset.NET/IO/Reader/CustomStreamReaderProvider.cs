using UnityAsset.NET.Enums;

namespace UnityAsset.NET.IO.Reader;

public class CustomStreamReaderProvider : IReaderProvider
{
    private readonly IStreamProvider _streamProvider;

    public CustomStreamReaderProvider(IStreamProvider streamProvider)
    {
        _streamProvider = streamProvider;
    }
    
    public IReader CreateReader(Endianness endian = Endianness.BigEndian) => new CustomStreamReader(_streamProvider, endian);
}