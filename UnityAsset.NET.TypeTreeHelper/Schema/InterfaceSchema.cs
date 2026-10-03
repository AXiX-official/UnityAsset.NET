namespace UnityAsset.NET.TypeTreeHelper.Schema;

/// <summary>One member of a generated interface, exactly as that interface declares it.</summary>
/// <param name="Name">The member name, as the interface spells it.</param>
/// <param name="DeclaredType">The declared type, verbatim, so a version-optional member keeps its "?".</param>
/// <param name="IsOptional">Whether some known version of the type lacks the member.</param>
public sealed record InterfaceMemberSchema(string Name, string DeclaredType, bool IsOptional);

/// <summary>
/// What one generated interface says: the name it is looked up by, whether it brings the INamedObject contract, and
/// the members it declares. <see cref="InterfaceSchemaTable.Entries"/> has one of these per interface the runtime
/// generator can meet, so it can read what the interfaces say instead of reflecting over them.
/// </summary>
public sealed record InterfaceSchema(string InterfaceName, bool DerivesFromNamedObject, IReadOnlyList<InterfaceMemberSchema> Members);
