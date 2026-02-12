namespace UnityAsset.NET.IO.Stream;

public class FileStreamProvider : IStreamProvider
{
    private readonly string Path;
    private readonly FileMode Mode;
    private readonly FileAccess Access;
    private readonly FileShare Share;

    public FileStreamProvider(string path,
        FileMode mode = FileMode.Open,
        FileAccess access = FileAccess.Read,
        FileShare share = FileShare.Read)
    {
        Path = path;
        Mode = mode;
        Access = access;
        Share = share;
    }

    public System.IO.Stream OpenStream() => new FileStream(Path, Mode, Access, Share);
}