namespace UnityAsset.NET.TypeTreeHelper;

/// <summary>
/// One type a session generates: the Unity class name it belongs to, the Unity version whose tree it carries, and
/// that tree. The class name is not the tree's own: a class that adds no fields of its own reuses its base's tree,
/// so the tree's root type name can be the base's name instead.
/// </summary>
public readonly record struct UnityTypeSource(string ClassName, string UnityVersion, TypeTreeRepr TypeTree);
