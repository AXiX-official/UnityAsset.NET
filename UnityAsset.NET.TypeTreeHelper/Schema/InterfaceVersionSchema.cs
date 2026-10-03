namespace UnityAsset.NET.TypeTreeHelper.Schema;

/// <summary>One member of an interface at one Unity version, with the type that version resolves it to.</summary>
public sealed record InterfaceVersionMember(string Name, string DeclaredType);

/// <summary>What one interface declares at one Unity version.</summary>
public sealed record InterfaceVersionSchema(string UnityVersion, IReadOnlyList<InterfaceVersionMember> Members);
