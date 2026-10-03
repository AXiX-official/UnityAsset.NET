namespace UnityAsset.NET.TypeTreeHelper.Compiler;

public sealed class LocalNames
{
    private readonly Dictionary<string, string> _declared = new(StringComparer.Ordinal);
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);

    /// <summary>Keeps a name the emitter writes itself, so an allocated one cannot collide with it.</summary>
    public void Reserve(string name) => _used.Add(name);

    /// <summary>The identifier for this path; the same path always yields the same name.</summary>
    public string Declare(LocalPath path)
    {
        var hint = path.Text;
        if (_declared.TryGetValue(hint, out var declared))
            return declared;

        var name = Helper.SanitizeName(hint);
        var candidate = name;
        for (var suffix = 2; !_used.Add(candidate); suffix++)
            candidate = $"{name}_{suffix}";

        _declared[hint] = candidate;
        return candidate;
    }
}
