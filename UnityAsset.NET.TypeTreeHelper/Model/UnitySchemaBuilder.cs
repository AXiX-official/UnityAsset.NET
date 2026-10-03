using AssetRipper.Primitives;

namespace UnityAsset.NET.TypeTreeHelper.Model;

/// <summary>Builds the semantic model from the type trees one generation run works on.</summary>
public static class UnitySchemaBuilder
{
    /// <summary>The offline case: every known version of one class.</summary>
    public static UnityTypeSchema Build(string name, List<(UnityVersion version, TypeTreeRepr node)> trees)
    {
        var versions = new List<UnityClassVersion>(trees.Count);
        foreach (var (version, tree) in trees)
            versions.Add(new UnityClassVersion(version.ToString(), tree, Members(tree)));

        return new UnityTypeSchema(name, versions);
    }

    /// <summary>The runtime case: the one tree a file carries, or the type tree database tree when it is stripped.</summary>
    public static UnityTypeSchema Build(string name, string version, TypeTreeRepr tree)
        => new(name, [new UnityClassVersion(version, tree, Members(tree))]);

    /// <summary>A type the runtime resolves while walking a class: one tree, with no version of its own.</summary>
    public static UnityTypeSchema Build(string name, TypeTreeRepr tree) => Build(name, string.Empty, tree);

    private static List<UnityMember> Members(TypeTreeRepr tree)
    {
        var members = new List<UnityMember>(tree.SubNodes.Length);
        foreach (var node in tree.SubNodes)
            members.Add(new UnityMember(
                node.Name,
                node.TypeName,
                UnityTypeResolver.GetTypeName(node),
                node.RequiresAlign,
                node));

        return members;
    }
}
