namespace UnityAsset.NET.FileSystem
{
    /// <summary>
    /// One stage of a load: it takes the files opened so far and returns the files the next stage should see.
    /// This is used when a game encrypted their files
    /// </summary>
    public interface IVirtualFileTransform
    {
        public string Name { get; }

        public IFileSystem.ErrorHandler? OnError { get; set; }

        public Task<List<IVirtualFileInfo>> TransformAsync(List<IVirtualFileInfo> files,
            IProgress<LoadProgress>? progress = null);
    }

    /// <summary>
    /// The common case: a stage that only understands containers, so it is interested in exactly what the sniffer
    /// recognised as one.
    /// </summary>
    public abstract class VirtualFileTransform : IVirtualFileTransform
    {
        public abstract string Name { get; }

        public IFileSystem.ErrorHandler? OnError { get; set; }

        public abstract Task<List<IVirtualFileInfo>> TransformAsync(List<IVirtualFileInfo> files,
            IProgress<LoadProgress>? progress = null);
    }
}
