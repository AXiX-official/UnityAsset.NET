using UnityAsset.NET.TypeTreeHelper.Compiler;

namespace UnityAsset.NET.TypeTreeHelper.Model;

/// <summary>One generated type at one Unity version: the tree it came from and the members that tree states.</summary>
public sealed record UnityClassVersion(string UnityVersion, TypeTreeRepr Tree, IReadOnlyList<UnityMember> Members);

/// <summary>A member as the union over versions presents it: one name, one type, and whether a version lacks it.</summary>
public sealed record UnityMemberProjection(string Name, string ResolvedType, bool IsVersionOptional);

/// <summary>
/// The semantic model of one generated type: the name it is generated under and every version known for it. The
/// emitters read this instead of walking type trees themselves.
/// </summary>
public sealed record UnityTypeSchema(string Name, IReadOnlyList<UnityClassVersion> Versions)
{
    /// <summary>Whether every known version carries m_Name, so the interface gets the name from INamedObject.</summary>
    public bool IsNamedAsset => Versions.All(version => Helper.IsNamedAsset(version.Tree));

    /// <summary>
    /// The members over every version, in first-seen order: one entry per name, its type merged across versions, and
    /// a named asset's own m_Name left out because the interface gets it from INamedObject.
    /// </summary>
    public IReadOnlyList<UnityMemberProjection> Members()
    {
        var isNamedAsset = IsNamedAsset;
        var order = new List<string>();
        var types = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var version in Versions)
        foreach (var member in version.Members)
        {
            if (isNamedAsset && member.Name == "m_Name")
                continue;

            if (!types.TryGetValue(member.Name, out var seen))
            {
                seen = [];
                types[member.Name] = seen;
                order.Add(member.Name);
            }

            seen.Add(member.ResolvedType);
        }

        var members = new List<UnityMemberProjection>(order.Count);
        foreach (var name in order)
        {
            var seen = types[name];
            members.Add(new UnityMemberProjection(
                name,
                UnityTypeResolver.GetUnionType(seen),
                seen.Count != Versions.Count));
        }

        return members;
    }
}
