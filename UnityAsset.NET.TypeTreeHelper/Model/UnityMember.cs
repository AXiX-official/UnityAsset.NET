namespace UnityAsset.NET.TypeTreeHelper.Model;

/// <summary>
/// One member of a class at one Unity version: as the tree states it, plus the C# type this one version resolves to.
/// </summary>
public sealed record UnityMember(string Name, string UnityTypeName, string ResolvedType, bool RequiresAlign, TypeTreeRepr Node);
