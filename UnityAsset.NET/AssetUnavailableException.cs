namespace UnityAsset.NET;

/// <summary>
/// Thrown instead of the underlying failure when a session is allowed to skip an asset.
/// <para>
/// The alternative — returning null, or an object whose members kept their defaults — cannot be told apart from "not
/// read yet" or from a real all-default asset, so a caller would have no way to know that the bytes were never decoded.
/// This type carries the identity and the reason so the failure stays local and diagnosable; the original exception is
/// kept as <see cref="Exception.InnerException"/>.
/// </para>
/// </summary>
public sealed class AssetUnavailableException : Exception
{
    public AssetUnavailableException(string typeName, long pathId, string reason, Exception? innerException = null)
        : base($"Asset {typeName}/{pathId} is unavailable: {reason}", innerException)
    {
        TypeName = typeName;
        PathId = pathId;
        Reason = reason;
    }

    public string TypeName { get; }

    public long PathId { get; }

    public string Reason { get; }
}
