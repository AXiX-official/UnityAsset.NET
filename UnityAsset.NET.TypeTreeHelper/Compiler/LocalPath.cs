namespace UnityAsset.NET.TypeTreeHelper.Compiler;

/// <summary>
/// Where a generated local sits: the member it belongs to plus the roles between it and that member. Roles are a
/// small vocabulary, so no emitter composes an identifier out of an expression; turning a path into a valid, unique
/// name is <see cref="LocalNames"/>' job.
/// </summary>
public readonly record struct LocalPath(string Text)
{
    public static LocalPath Root(string memberName) => new(memberName);

    /// <summary>The AssetNode built for a vector member.</summary>
    public LocalPath VectorNode => Append("VectorNode");

    /// <summary>The AssetNode built for a map member.</summary>
    public LocalPath MapNode => Append("MapNode");

    /// <summary>The AssetNode built for a pair, which is a map entry or a pair-typed member.</summary>
    public LocalPath PairNode => Append("PairNode");

    /// <summary>The AssetNode built for a class, predefined or PPtr member.</summary>
    public LocalPath ChildNode => Append("ChildNode");

    /// <summary>The element a vector loop is walking.</summary>
    public LocalPath Item => Append("item");

    /// <summary>The pair a map loop is walking.</summary>
    public LocalPath Pair => Append("pair");

    /// <summary>A pair's first part.</summary>
    public LocalPath Item1 => Append("Item1");

    /// <summary>A pair's second part.</summary>
    public LocalPath Item2 => Append("Item2");

    private LocalPath Append(string role) => new($"{Text}_{role}");

    public override string ToString() => Text;
}
