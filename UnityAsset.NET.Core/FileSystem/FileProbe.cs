namespace UnityAsset.NET.FileSystem
{
    /// <summary>
    /// What the bytes at the start of a file say it is, and — when they say nothing — why.
    /// <para>
    /// The reason exists because "unknown" is not a diagnosis: a file nobody could name is either too short to judge, or
    /// shaped like a serialized file whose size disagrees, or some other format entirely, and a caller can only act on
    /// the difference if the probe says which. This is a struct rather than a record because the Core library is compiled
    /// for netstandard2.1, where the init accessors a record needs do not exist.
    /// </para>
    /// </summary>
    public readonly struct FileProbe
    {
        public FileProbe(FileType type, string? reason)
        {
            Type = type;
            Reason = reason;
        }

        public FileType Type { get; }

        /// <summary>Why the probe did not recognise the file, or null when it did.</summary>
        public string? Reason { get; }

        /// <summary>Whether the probe recognised the file as something a session can open as a file of its own.</summary>
        public bool IsRecognised => Type is FileType.BundleFile or FileType.SerializedFile;

        /// <summary>A recognised file: no reason to give.</summary>
        public static FileProbe Of(FileType type) => new(type, null);
    }
}
