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

        /// <summary>
        /// Whether this stage could use a file it has not materialised yet. An archive asks this before it inflates an
        /// entry: an entry that the sniffer did not recognise and that no stage claims is never read past its header,
        /// which is what keeps a whole package from being copied into memory to be thrown away again.
        /// </summary>
        public bool IsInterested(string name, long length, FileType fileType);

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

        public virtual bool IsInterested(string name, long length, FileType fileType)
            => fileType != FileType.Unknown;

        public abstract Task<List<IVirtualFileInfo>> TransformAsync(List<IVirtualFileInfo> files,
            IProgress<LoadProgress>? progress = null);
    }
}
