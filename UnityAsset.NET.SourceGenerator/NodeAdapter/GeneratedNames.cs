namespace UnityAsset.NET.SourceGenerator.NodeAdapter;

/// <summary>
/// The names the generated file uses for a type. They live in one place because the classes, the three functions and
/// every reference between them have to agree, and because a reader of the generated file should only have to learn one
/// convention. The identifier is derived from the type itself, so a name says what it adapts instead of pointing at an
/// index that has to be looked up.
/// </summary>
internal static class GeneratedNames
{
    public static string Read(string identifier) => "Read_" + identifier;

    public static string Write(string identifier) => "Write_" + identifier;

    public static string Match(string identifier) => "Match_" + identifier;

    public static string Adapter(string identifier) => "Adapter_" + identifier;
}
