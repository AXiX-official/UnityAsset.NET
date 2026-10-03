namespace UnityAsset.NET.TypeTreeHelper.Schema;

/// <summary>
/// What the runtime generator can read about the interfaces: the table generation writes for the interfaces it emits,
/// plus the hand-written interfaces it still looks up, whose declarations cannot be generated.
/// </summary>
public static class InterfaceSchemaTable
{
    public static readonly IReadOnlyDictionary<string, InterfaceSchema> Entries = Merge();

    private static Dictionary<string, InterfaceSchema> Merge()
    {
        var entries = new Dictionary<string, InterfaceSchema>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, schema) in HandWrittenInterfaceSchema.Entries)
            entries[name] = schema;

        foreach (var (name, schema) in GeneratedInterfaceSchemaTable.Entries)
            entries[name] = schema;

        return entries;
    }
}
